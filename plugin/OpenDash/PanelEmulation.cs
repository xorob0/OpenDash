// PanelEmulation.cs: what the Rig page paints on every tile when a driver picks a flag, a car alongside,
// the pit lane, a warning or the revs -- and the still pictures Home, LEDs and Matrix draw of a device.
//
// The only way to check a flag used to be to own the hardware and wait for one (#503). The Rig page paints
// the state on every tile at once instead, from the same sources the devices are built from: a matrix
// from the flag box's glyph sheet (PanelGlyphSheet, which is the generator's own drawing), a strip by the
// rules Rig.dc.html scripts, and a face's band in the dash's own words. It is a picture of what the rig
// would show, never a command to it; lighting the real hardware is #506.
//
// Every colour is a Theme constant, which ThemeTests holds to design/tokens.json. A cell or an LED that is
// dark is null here, and the drawing paints it in its style's unlit colour.
//
// Pure: no WPF. PanelEmulationTests holds the frames to the artboard's rules and the glyph names to the
// sheet the build writes.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One chip on the Rig page: something that can happen, and the colour it is shown by.</summary>
    public sealed class EmulationScenario
    {
        public EmulationScenario(string id, string label, string swatchHex, string group)
        {
            Id = id;
            Label = label;
            SwatchHex = swatchHex;
            Group = group;
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public string SwatchHex { get; private set; }
        public string Group { get; private set; }
    }

    /// <summary>One group of chips, with its heading.</summary>
    public sealed class EmulationGroup
    {
        public EmulationGroup(string title, params EmulationScenario[] scenarios)
        {
            Title = title;
            Scenarios = scenarios;
        }

        public string Title { get; private set; }
        public IReadOnlyList<EmulationScenario> Scenarios { get; private set; }
    }

    /// <summary>What a strip is set to that changes its picture.</summary>
    public sealed class StripOptions
    {
        public StripOptions()
        {
            EffectsOff = new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>A car alongside lights the whole strip rather than its nearer end.</summary>
        public bool SpotterWhole { get; set; }

        /// <summary>The effect ids (Contract.LedEffects) the strip does not draw.</summary>
        public ISet<string> EffectsOff { get; set; }

        /// <summary>What the strip's centre shows, one of Contract.LedCentres (OpenDashSettings.BarCentre); null
        /// reads as the revs, the default. Only the revs centre draws the rev ladder.</summary>
        public string Centre { get; set; }

        /// <summary>Whether the centre carries the rev ladder: its Centre display is the revs.</summary>
        public bool CentreShowsRevs
        {
            get { return Contract.NormaliseLedCentre(Centre) == Contract.DefaultLedCentre; }
        }

        public static readonly StripOptions Default = new StripOptions();

        public bool Draws(string effectId)
        {
            if (EffectsOff == null || EffectsOff.Count == 0) return true;
            if (EffectsOff.Contains(effectId)) return false;
            // Every flag row is one switch, so a flag id is off when any flag id is.
            if (effectId.StartsWith(Contract.LedEffectFlagPrefix, StringComparison.Ordinal))
            {
                return !EffectsOff.Any(id => id != null && id.StartsWith(Contract.LedEffectFlagPrefix, StringComparison.Ordinal));
            }
            return true;
        }
    }

    /// <summary>What a matrix panel is set to that changes its picture.</summary>
    public sealed class MatrixOptions
    {
        public MatrixOptions()
        {
            Side = Contract.FlagBoxSides[0];
            Rest = Contract.FlagBoxRests[0];
            Bands = true;
            Flags = true;
            Pit = true;
            Spotter = true;
            Warnings = true;
        }

        /// <summary>The mounting side, one of Contract.FlagBoxSides.</summary>
        public string Side { get; set; }

        /// <summary>The idle display, one of Contract.FlagBoxRests.</summary>
        public string Rest { get; set; }

        /// <summary>Whether the gear changes colour with the revs.</summary>
        public bool Bands { get; set; }

        /// <summary>Whether the gear takes the car's own thresholds. The picture is the same either way; it
        /// is carried so a caller can pass a panel's settings whole.</summary>
        public bool CarLadder { get; set; }

        public bool Flags { get; set; }
        public bool Pit { get; set; }
        public bool Spotter { get; set; }
        public bool Warnings { get; set; }
    }

    /// <summary>A face's band under a scenario: its ground, its words, and how it is drawn.</summary>
    public sealed class FaceBand
    {
        public FaceBand(string fillHex, string text, string textHex, bool outlined, bool chequer)
        {
            FillHex = fillHex;
            Text = text;
            TextHex = textHex;
            Outlined = outlined;
            Chequer = chequer;
        }

        /// <summary>The band's ground, or null when nothing takes the band and it shows its own page.</summary>
        public string FillHex { get; private set; }

        /// <summary>The band's words, as the dash writes them, or null.</summary>
        public string Text { get; private set; }

        public string TextHex { get; private set; }

        /// <summary>Drawn as an outline of the colour on the face's own ground, as the black flag is.</summary>
        public bool Outlined { get; private set; }

        /// <summary>Drawn as a chequer, as the chequered flag is, with no words.</summary>
        public bool Chequer { get; private set; }

        /// <summary>Whether a scenario has taken the band.</summary>
        public bool Alert { get { return FillHex != null || Chequer; } }

        public static readonly FaceBand Idle = new FaceBand(null, null, Theme.TextSecondary, false, false);
    }

    /// <summary>How a strip is drawn at one of the sizes the pages use.</summary>
    public sealed class StripStyle
    {
        public StripStyle(double led, double radius, double gap, double groupGap, double padX, double padY, string groundHex, string borderHex, double cornerRadius, string unlitHex)
        {
            Led = led;
            Radius = radius;
            Gap = gap;
            GroupGap = groupGap;
            PadX = padX;
            PadY = padY;
            GroundHex = groundHex;
            BorderHex = borderHex;
            CornerRadius = cornerRadius;
            UnlitHex = unlitHex;
        }

        public double Led { get; private set; }
        public double Radius { get; private set; }
        public double Gap { get; private set; }
        public double GroupGap { get; private set; }
        public double PadX { get; private set; }
        public double PadY { get; private set; }

        /// <summary>The ground behind the LEDs, or null for none.</summary>
        public string GroundHex { get; private set; }

        /// <summary>The frame round them, or null for none.</summary>
        public string BorderHex { get; private set; }

        public double CornerRadius { get; private set; }
        public string UnlitHex { get; private set; }

        /// <summary>Home's "Right now": 11 px LEDs on the inset ground.</summary>
        public static readonly StripStyle Home = new StripStyle(11, 2, 2, 7, 8, 7, Theme.SurfaceInset, null, Theme.Radius, Theme.SurfaceRaised);

        /// <summary>A tile on the Rig page: 14 px LEDs in a framed box.</summary>
        public static readonly StripStyle Rig = new StripStyle(14, 3, 3, 8, 8, 6, Theme.SurfaceBase, Theme.Border, 3, Theme.SurfaceRaised);

        /// <summary>A strip's card on LEDs: 9 px, no ground of its own.</summary>
        public static readonly StripStyle Card = new StripStyle(9, 2, 2, 6, 0, 0, null, null, 0, Theme.SurfaceRaised);

        /// <summary>The big preview on a strip's page: 30 px.</summary>
        public static readonly StripStyle Preview = new StripStyle(30, 3, 6, 24, 0, 0, null, null, 0, Theme.SurfaceRaised);

        /// <summary>The shape picture in the Add LEDs sheet: 14 px on the inset ground.</summary>
        public static readonly StripStyle AddLeds = new StripStyle(14, 2, 3, 8, 8, 8, Theme.SurfaceInset, null, Theme.Radius, Theme.SurfaceRaised);
    }

    /// <summary>How an 8x8 is drawn at one of the sizes the pages use.</summary>
    public sealed class MatrixStyle
    {
        public MatrixStyle(double cell, double gap, double pad, string groundHex, string borderHex, double cornerRadius, string unlitHex)
        {
            Cell = cell;
            Gap = gap;
            Pad = pad;
            GroundHex = groundHex;
            BorderHex = borderHex;
            CornerRadius = cornerRadius;
            UnlitHex = unlitHex;
        }

        public double Cell { get; private set; }
        public double Gap { get; private set; }
        public double Pad { get; private set; }
        public string GroundHex { get; private set; }
        public string BorderHex { get; private set; }
        public double CornerRadius { get; private set; }
        public string UnlitHex { get; private set; }

        /// <summary>The whole grid's side, padding included.</summary>
        public double Side { get { return 8 * Cell + 7 * Gap + 2 * Pad; } }

        public static readonly MatrixStyle Home = new MatrixStyle(6, 1, 4, Theme.SurfaceInset, null, Theme.Radius, Theme.SurfaceZone);
        public static readonly MatrixStyle Rig = new MatrixStyle(10, 2, 6, Theme.SurfaceBase, Theme.Border, 3, Theme.SurfaceZone);
        public static readonly MatrixStyle Card = new MatrixStyle(5, 1, 4, Theme.SurfaceInset, null, 0, Theme.SurfaceZone);
        public static readonly MatrixStyle Preview = new MatrixStyle(26, 6, 0, null, null, 0, Theme.SurfaceZone);
    }

    public static class PanelEmulation
    {
        // Scenario ids, which the Rig page keeps in its route and a test names.
        public const string Green = "green";
        public const string Yellow = "yellow";
        public const string Blue = "blue";
        public const string White = "white";
        public const string Black = "black";
        public const string Chequer = "chequer";
        public const string Red = "red";
        public const string CarLeft = "left";
        public const string CarRight = "right";
        public const string CarBoth = "both";
        public const string Limiter = "limiter";
        public const string Speeding = "speeding";
        public const string LowFuel = "fuel";
        public const string Oil = "oil";
        public const string Water = "water";
        public const string Idle = "idle";
        public const string Mid = "mid";
        public const string Shift = "shift";

        /// <summary>What the Rig page opens on: the revs half way, which is a rig in motion and nothing wrong.</summary>
        public const string Default = Mid;

        /// <summary>
        /// The chips, in the five groups Rig.dc.html draws, in the words the rest of the panel uses:
        /// "Pit limiter" rather than "Limiter on", and the temperatures by name rather than "hot".
        /// </summary>
        public const string FlagsGroup = "Flags";
        public const string SpotterGroup = "Spotter";
        public const string PitLaneGroup = "Pit lane";
        public const string WarningsGroup = "Warnings";
        public const string RevsGroup = "Revs";

        public static readonly IReadOnlyList<EmulationGroup> Groups = new[]
        {
            new EmulationGroup(FlagsGroup,
                new EmulationScenario(Green, "Green", Theme.FlagGreen, FlagsGroup),
                new EmulationScenario(Yellow, "Yellow", Theme.FlagYellow, FlagsGroup),
                new EmulationScenario(Blue, "Blue", Theme.FlagBlue, FlagsGroup),
                new EmulationScenario(White, "White", Theme.FlagWhite, FlagsGroup),
                new EmulationScenario(Black, "Black", Theme.FlagBlack, FlagsGroup),
                new EmulationScenario(Chequer, "Chequered", Theme.FlagChequer, FlagsGroup),
                new EmulationScenario(Red, "Red", Theme.FlagRed, FlagsGroup)),
            new EmulationGroup(SpotterGroup,
                new EmulationScenario(CarLeft, "Car left", Theme.Caution, SpotterGroup),
                new EmulationScenario(CarRight, "Car right", Theme.Caution, SpotterGroup),
                new EmulationScenario(CarBoth, "Both sides", Theme.Caution, SpotterGroup)),
            new EmulationGroup(PitLaneGroup,
                new EmulationScenario(Limiter, "Pit limiter", Theme.PitLimiter, PitLaneGroup),
                new EmulationScenario(Speeding, "Speeding", Theme.Danger, PitLaneGroup)),
            new EmulationGroup(WarningsGroup,
                new EmulationScenario(LowFuel, "Low fuel", Theme.FlagYellow, WarningsGroup),
                new EmulationScenario(Oil, "Oil temperature", Theme.Caution, WarningsGroup),
                new EmulationScenario(Water, "Water temperature", Theme.Caution, WarningsGroup)),
            new EmulationGroup(RevsGroup,
                new EmulationScenario(Idle, "Idle", Theme.SurfaceRaised, RevsGroup),
                new EmulationScenario(Mid, "Mid revs", Theme.ShiftStage1, RevsGroup),
                new EmulationScenario(Shift, "Shift point", Theme.ShiftStage3, RevsGroup)),
        };

        public static IEnumerable<EmulationScenario> Scenarios()
        {
            return Groups.SelectMany(group => group.Scenarios);
        }

        public static EmulationScenario Find(string id)
        {
            return Scenarios().FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        }

        public static bool IsFlag(string id)
        {
            return id == Green || id == Yellow || id == Blue || id == White || id == Black || id == Chequer || id == Red;
        }

        /// <summary>The colour a flag lights an LED in. Black and the chequer light in the panel's white,
        /// since a black LED is an unlit one.</summary>
        public static string FlagColour(string id)
        {
            switch (id)
            {
                case Green: return Theme.FlagGreen;
                case Yellow: return Theme.FlagYellow;
                case Blue: return Theme.FlagBlue;
                case White: return Theme.FlagWhite;
                case Black: return Theme.FlagBlack;
                case Chequer: return Theme.FlagChequer;
                case Red: return Theme.FlagRed;
                default: return null;
            }
        }

        /// <summary>The effect id a scenario is drawn by on a strip, or null for the revs.</summary>
        private static string EffectOf(string id)
        {
            switch (id)
            {
                case Green: return "flag.green";
                case Yellow: return "flag.yellow";
                case Blue: return "flag.blue";
                case White: return "flag.white";
                case Black: return "flag.black";
                case Chequer: return "flag.chequered";
                case Red: return "flag.red";
                case Limiter: return "pit.limiter";
                case Speeding: return "pit.speeding";
                case LowFuel: return "lowFuel";
                case Oil: return "temperature";
                case Water: return "temperature";
                default: return null;
            }
        }

        /// <summary>How far up the rev ladder a scenario is: 0 at idle, 1 at the shift point, and six tenths
        /// for everything else, which is a car being driven.</summary>
        public static double RevLevel(string scenarioId)
        {
            if (scenarioId == Idle) return 0;
            return scenarioId == Shift ? 1 : 0.6;
        }

        /// <summary>
        /// The colour of each LED of a rev run of <paramref name="count"/> at a scenario, or null for unlit.
        /// </summary>
        /// <remarks>
        /// The first 45 per cent are stage one, to 80 per cent stage two, and the rest stage three, lit up to
        /// the scenario's level; at the shift point the whole run is stage three, which is the flash.
        /// </remarks>
        public static string[] Revs(int count, string scenarioId)
        {
            var leds = new string[Math.Max(0, count)];
            var level = RevLevel(scenarioId);
            for (var i = 0; i < leds.Length; i++)
            {
                if (scenarioId == Shift) { leds[i] = Theme.ShiftStage3; continue; }
                var f = (i + 1) / (double)leds.Length;
                if (f > level + 1e-9) { leds[i] = null; continue; }
                leds[i] = f <= 0.45 ? Theme.ShiftStage1 : f <= 0.8 ? Theme.ShiftStage2 : Theme.ShiftStage3;
            }
            return leds;
        }

        /// <summary>
        /// A strip's LEDs under a scenario, as groups: the left end, the centre and the right end, or the
        /// centre alone on a bare run.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Rig.dc.html's rules: a flag fills the ends, a car alongside lights its own side's end (both ends
        /// for both), the limiter alternates every LED, speeding reddens the ends, and the centre carries the revs. A bare run
        /// carries flags and the pit lane across itself, which is what the generator draws there
        /// (rpmStrip.ts); the artboard's brow leaves the flag off and is wrong about it. An effect the strip
        /// has switched off is not drawn.
        /// </para>
        /// <para>
        /// Where the generator differs from the artboard, the generator wins (#523). A bare run has no lamps,
        /// so a car alongside takes the whole run (rpmStrip.ts gives the side role the run where a shape has
        /// none). Low fuel and the temperature warning light each side's car lamp, the LED lampsForSide gives
        /// the car's own warnings (the one LED of a one-LED side, the inner of two, the third from the
        /// outside of three or more), in the fuel's low colour or the caution amber; a bare run has no car
        /// lamp and shows neither. The full-strip spotter lights every LED of the strip, the ends and the
        /// centre, for a side that is switched on, as rpmStrip.ts's spotterWhole does; the artboard lights only
        /// the centre's half on the car's side. A centre whose Centre display is not the revs
        /// (StripOptions.Centre) starts from its stand-in rather than the rev ladder, and every effect composes
        /// over it as over the revs.
        /// </para>
        /// </remarks>
        public static string[][] StripFrame(int ends, int centre, string scenarioId, StripOptions options = null)
        {
            options = options ?? StripOptions.Default;
            ends = Math.Max(0, ends);
            var left = new string[ends];
            var right = new string[ends];
            var middle = Centre(centre, scenarioId, options);
            var effect = EffectOf(scenarioId);
            var draws = effect == null || options.Draws(effect);

            if (IsFlag(scenarioId) && draws)
            {
                var colour = FlagColour(scenarioId);
                if (ends > 0) { Fill(left, colour); Fill(right, colour); }
                else Fill(middle, colour);
            }
            else if (ends == 0 && (scenarioId == CarLeft || scenarioId == CarRight || scenarioId == CarBoth))
            {
                var lightLeft = scenarioId != CarRight && options.Draws("spotter.left");
                var lightRight = scenarioId != CarLeft && options.Draws("spotter.right");
                if (lightLeft || lightRight) Fill(middle, Theme.Caution);
            }
            else if (scenarioId == CarLeft || scenarioId == CarRight || scenarioId == CarBoth)
            {
                var lightLeft = scenarioId != CarRight && options.Draws("spotter.left");
                var lightRight = scenarioId != CarLeft && options.Draws("spotter.right");
                if (options.SpotterWhole && (lightLeft || lightRight))
                {
                    // Every LED of the strip, the ends and the centre, as rpmStrip.ts's spotterWhole draws a
                    // side that is switched on over the whole run.
                    Fill(left, Theme.Caution);
                    Fill(middle, Theme.Caution);
                    Fill(right, Theme.Caution);
                }
                else
                {
                    if (lightLeft) Fill(left, Theme.Caution);
                    if (lightRight) Fill(right, Theme.Caution);
                }
            }
            else if (scenarioId == Limiter && draws)
            {
                Alternate(left, Theme.PitLimiter);
                Alternate(right, Theme.PitLimiter);
                Alternate(middle, Theme.PitLimiter);
            }
            else if (scenarioId == Speeding && draws)
            {
                if (ends > 0) { Fill(left, Theme.Danger); Fill(right, Theme.Danger); }
                else Fill(middle, Theme.Danger);
            }
            else if ((scenarioId == LowFuel || scenarioId == Oil || scenarioId == Water) && draws && ends > 0)
            {
                var lamp = CarLamp(ends);
                var colour = scenarioId == LowFuel ? Theme.FuelLow : Theme.Caution;
                left[lamp] = colour;
                right[ends - 1 - lamp] = colour;
            }

            return ends > 0 ? new[] { left, middle, right } : new[] { middle };
        }

        /// <summary>
        /// Which LED of an end, counted from the outside, is the car lamp, which carries the car's own
        /// warnings: lampsForSide's (lamps.ts) "car" role, shared with the side and race roles on a one-LED
        /// side, with the race role on a two-LED side, and the third lamp of every longer side.
        /// </summary>
        public static int CarLamp(int ends)
        {
            return Math.Max(0, Math.Min(ends - 1, 2));
        }

        /// <summary>
        /// A centre before any effect is drawn over it: the rev ladder where its Centre display is the revs,
        /// and otherwise a stand-in for what it shows, which the emulation has no value for. A brake bar is dark,
        /// the pedals at rest; the throttle and brake bar lights only its standing mark, the middle LED of an
        /// odd run, which the profile lights always; the fuel gauge is dark except under Low fuel, where the
        /// tank's last LED is lit in the low colour, as the gauge blinks there (rpmStrip.ts fuelBar), when
        /// the strip draws Low fuel.
        /// </summary>
        public static string[] Centre(int count, string scenarioId, StripOptions options = null)
        {
            options = options ?? StripOptions.Default;
            if (options.CentreShowsRevs) return Revs(count, scenarioId);
            var leds = new string[Math.Max(0, count)];
            var shows = Contract.NormaliseLedCentre(options.Centre);
            if (shows == "throttleBrake" && leds.Length % 2 == 1) leds[leds.Length / 2] = Theme.TextPrimary;
            if (shows == "fuel" && scenarioId == LowFuel && options.Draws("lowFuel") && leds.Length > 0) leds[0] = Theme.FuelLow;
            return leds;
        }

        /// <summary>The width of one colour in a packed run: "#AARRGGBB".</summary>
        public const int PackedColourWidth = 9;

        /// <summary>
        /// A strip's LEDs as the car's own rev lights have them now: the centre from the packed run
        /// <c>plugin.CarLights.Run(centre)</c> returns, the ends unlit, grouped as <see cref="StripFrame"/>
        /// groups them. Home's live strip and the LEDs page's "Live" chip both draw this, so the two read the
        /// run one way.
        /// </summary>
        /// <remarks>
        /// The run is fixed-width "#AARRGGBB" colours end to end (CarLightService.Packed). A colour with a zero
        /// alpha is a gap in the car's bar and is drawn unlit (null), as CarLightMirror.IsTransparent reads it.
        /// Run returns "" for a length not in Contract.MirrorRunLengths, and a run that is empty or not
        /// <paramref name="centre"/> colours long is drawn dark rather than guessed at.
        /// </remarks>
        public static string[][] LiveFrame(string packed, int ends, int centre)
        {
            ends = Math.Max(0, ends);
            centre = Math.Max(0, centre);
            var left = new string[ends];
            var right = new string[ends];
            var middle = new string[centre];
            if (packed != null && centre > 0 && packed.Length == centre * PackedColourWidth)
            {
                for (var i = 0; i < centre; i++)
                {
                    var colour = packed.Substring(i * PackedColourWidth, PackedColourWidth);
                    middle[i] = colour[0] != '#' || CarLightMirror.IsTransparent(colour) ? null : colour;
                }
            }
            return ends > 0 ? new[] { left, middle, right } : new[] { middle };
        }

        /// <summary>
        /// An 8x8 under a scenario, as 64 cells row by row, from the glyph the flag box would show: null for a
        /// dark cell.
        /// </summary>
        /// <remarks>
        /// A family the panel has switched off, or a car on the side the panel is not mounted on, leaves the
        /// panel at its idle display: the gear when that is what it rests on, dark otherwise. The still of a
        /// glyph that moves is the frame it holds longest (PanelGlyph.Still). Without a sheet every frame is
        /// dark, which is what a build that did not embed one has to draw.
        /// </remarks>
        public static string[] MatrixFrame(PanelGlyphSheet sheet, string scenarioId, MatrixOptions options = null)
        {
            options = options ?? new MatrixOptions();
            var name = GlyphFor(scenarioId, options);
            return Cells(sheet, name);
        }

        /// <summary>The name of the glyph a panel shows for a scenario, or null for dark.</summary>
        public static string GlyphFor(string scenarioId, MatrixOptions options = null)
        {
            options = options ?? new MatrixOptions();
            string name = null;
            if (IsFlag(scenarioId) && options.Flags) name = FlagGlyph(scenarioId);
            else if ((scenarioId == CarLeft || scenarioId == CarRight || scenarioId == CarBoth) && options.Spotter) name = SpotterGlyph(scenarioId, options.Side);
            else if (scenarioId == Limiter && options.Pit) name = "Pit limiterInLane";
            else if (scenarioId == Speeding && options.Pit) name = "Pit speeding";
            else if (scenarioId == LowFuel && options.Warnings) name = "Warning lowFuel";
            else if (scenarioId == Oil && options.Warnings) name = "Warning oilHot";
            else if (scenarioId == Water && options.Warnings) name = "Warning waterHot";
            return name ?? RestGlyph(scenarioId, options);
        }

        /// <summary>The gear the panels are drawn showing.</summary>
        public const string Gear = "4";

        private static string FlagGlyph(string id)
        {
            switch (id)
            {
                case Chequer: return "chequered";
                default: return id;
            }
        }

        private static string SpotterGlyph(string id, string side)
        {
            var mounted = (side ?? string.Empty).ToLowerInvariant();
            var left = id != CarRight && mounted != "right";
            var right = id != CarLeft && mounted != "left";
            if (left && right) return "Spotter carBoth";
            if (left) return "Spotter carLeft";
            return right ? "Spotter carRight" : null;
        }

        private static string RestGlyph(string scenarioId, MatrixOptions options)
        {
            if (!string.Equals(options.Rest, "gear", StringComparison.OrdinalIgnoreCase)) return null;
            if (!options.Bands || scenarioId == Idle) return "Gear " + Gear + " rest";
            return scenarioId == Shift ? "Gear " + Gear + " redline" : "Gear " + Gear + " stage1";
        }

        /// <summary>A glyph's still as 64 cells, or 64 dark ones when the sheet has no such glyph.</summary>
        public static string[] Cells(PanelGlyphSheet sheet, string glyphName)
        {
            var cells = new string[64];
            if (sheet == null || glyphName == null) return cells;
            var glyph = sheet.Find(glyphName);
            var still = glyph == null ? null : glyph.Still;
            if (still == null) return cells;
            for (var r = 0; r < 8 && r < still.Length; r++)
            {
                for (var c = 0; c < 8 && c < still[r].Length; c++) cells[r * 8 + c] = still[r][c];
            }
            return cells;
        }

        /// <summary>
        /// A face's band under a scenario, in the dash's own words: "YELLOW", "WHITE · LAST LAP", "Pit
        /// limiter". Anything the dash does not put on its band leaves the band to its own page.
        /// </summary>
        public static FaceBand Band(string scenarioId)
        {
            switch (scenarioId)
            {
                case Green: return new FaceBand(Theme.FlagGreen, "GREEN", Theme.OnFlag, false, false);
                case Yellow: return new FaceBand(Theme.FlagYellow, "YELLOW", Theme.OnFlag, false, false);
                case Blue: return new FaceBand(Theme.FlagBlue, "BLUE", Theme.OnFlag, false, false);
                case White: return new FaceBand(Theme.FlagWhite, "WHITE · LAST LAP", Theme.OnFlag, false, false);
                case Black: return new FaceBand(Theme.FlagBlack, "BLACK", Theme.FlagBlack, true, false);
                case Chequer: return new FaceBand(null, null, Theme.OnFlag, false, true);
                case Red: return new FaceBand(Theme.FlagRed, "RED", Theme.OnFlag, false, false);
                case Limiter: return new FaceBand(Theme.PitLimiter, "Pit limiter", Theme.OnFlag, false, false);
                default: return FaceBand.Idle;
            }
        }

        /// <summary>The ring round a round face: the revs' colour, unlit at idle.</summary>
        public static string RingColour(string scenarioId)
        {
            if (scenarioId == Shift) return Theme.ShiftStage3;
            return scenarioId == Idle ? Theme.SurfaceRaised : Theme.ShiftStage1;
        }

        /// <summary>
        /// How bright the lights are drawn: the night brightness as a fraction while night mode is on, full
        /// otherwise. The screens do not dim with it; that is #128.
        /// </summary>
        public static double Dim(bool night, int nightBrightness)
        {
            if (!night) return 1;
            var percent = nightBrightness < 0 ? 0 : nightBrightness > 100 ? 100 : nightBrightness;
            return percent / 100.0;
        }

        /// <summary>
        /// How bright the lights are drawn when the picture follows the day brightness too: the day
        /// brightness by day, the night brightness at night. The Settings page's preview, whose ruling takes
        /// its opacity from both, draws this; the pictures that dim only at night keep the two-argument Dim.
        /// </summary>
        public static double Dim(bool night, int dayBrightness, int nightBrightness)
        {
            var value = night ? nightBrightness : dayBrightness;
            var percent = value < 0 ? 0 : value > 100 ? 100 : value;
            return percent / 100.0;
        }

        private static void Fill(string[] leds, string colour)
        {
            for (var i = 0; i < leds.Length; i++) leds[i] = colour;
        }

        private static void Alternate(string[] leds, string colour)
        {
            for (var i = 0; i < leds.Length; i++) leds[i] = i % 2 == 1 ? null : colour;
        }
    }
}
