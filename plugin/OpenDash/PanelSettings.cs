// PanelSettings.cs: the Settings page's words and decisions -- its sections and their order, the "On this page"
// row, the units SimHub is set to, the alert table, the lighting preview, and what search finds.
//
// Settings holds only what is the same everywhere: the race data, the flags, the alert thresholds, the
// lighting, and the rows #99, #127, #128, #129, #484 and #104 will fill. The race data's words stay in
// PanelDataTab, where their pins are. SettingsControl.Settings.cs draws what this file decides and decides
// nothing itself; PanelSettingsTests holds it to Settings.dc.html and to docs/design/voice.md.
//
// Pure: no WPF, no SimHub. SimHub's units arrive as the enum names GameReaderCommon spells them ("Kmh",
// "Celcius", "Bar", "Liters"), which the page reads and passes in as text.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One line of the alert table: what warns, at what, and where it shows today.</summary>
    public sealed class SettingsAlert
    {
        public SettingsAlert(string title, string op, string unit, string example, bool screens, bool leds, bool matrix, bool racesOnly, string scenarioId, string anchor = null)
        {
            Title = title;
            Op = op;
            Unit = unit;
            Example = example;
            Screens = screens;
            Leds = leds;
            Matrix = matrix;
            RacesOnly = racesOnly;
            ScenarioId = scenarioId;
            Anchor = anchor;
        }

        /// <summary>The row's name, which is the registry's title on a greyed row.</summary>
        public string Title { get; private set; }

        /// <summary>The word before the box: "under", "over" or "at". Empty on a row with no threshold.</summary>
        public string Op { get; private set; }

        /// <summary>The unit after the box, or null for a temperature, whose unit is SimHub's; empty where there is
        /// none. Drawn alone as a caption, so every unit holds a letter: the tyre wear's percent sign, which
        /// has none, sits inside its example's "70%" rather than after the box, as the panel joins every
        /// percentage to its number.</summary>
        public string Unit { get; private set; }

        /// <summary>What a greyed row's box holds as its value, in the field's own ink and faded with the row,
        /// so the row reads as the setting it will be. Not a placeholder: the artboard sets it as the value.
        /// Null where the artboard has none, and the box shows the artboard's dash, drawn.</summary>
        public string Example { get; private set; }

        /// <summary>Whether a word stands before the box. Pit window open, an event, has none, and its box is
        /// still drawn, empty, as the artboard draws it.</summary>
        public bool HasThreshold { get { return !string.IsNullOrEmpty(Op); } }

        public bool Screens { get; private set; }
        public bool Leds { get; private set; }
        public bool Matrix { get; private set; }
        public bool RacesOnly { get; private set; }

        /// <summary>The Rig page's scenario "Try" opens, or null on a row that is not built.</summary>
        public string ScenarioId { get; private set; }

        /// <summary>Where a search for a live row lands, its name cell; null on a greyed row, whose cells carry
        /// its ticket's anchor.</summary>
        public string Anchor { get; private set; }

        /// <summary>Whether the row is live: a greyed row has nothing to try.</summary>
        public bool Live { get { return ScenarioId != null; } }

        /// <summary>The four surface columns, in the table's order.</summary>
        public bool[] Surfaces { get { return new[] { Screens, Leds, Matrix, RacesOnly }; } }
    }

    public static class PanelSettings
    {
        public const string Title = "Settings";

        // --- Sections --------------------------------------------------------------------------------

        public const string FlagsTitle = "Flags";
        public const string AlertsTitle = "Alerts";
        public const string LightingTitle = "Lighting";
        public const string AppearanceTitle = "Appearance";
        public const string DriverTitle = "Driver";

        public const string AnchorRaceData = "settings.race-data";
        public const string AnchorFlags = "settings.flags";
        public const string AnchorAlerts = "settings.alerts";
        public const string AnchorLighting = "settings.lighting";
        public const string AnchorAppearance = "settings.appearance";
        public const string AnchorDriver = "settings.driver";

        /// <summary>The sections in the page's order, which is the "On this page" row's order too.</summary>
        public static readonly string[] SectionTitles = { PanelDataTab.SectionTitle, FlagsTitle, AlertsTitle, LightingTitle, AppearanceTitle, DriverTitle };

        public static readonly string[] SectionAnchors = { AnchorRaceData, AnchorFlags, AnchorAlerts, AnchorLighting, AnchorAppearance, AnchorDriver };

        // Every live row's own anchor, which its search entry lands on: a section's anchor brings the whole
        // section into view, and where the section is taller than the view WPF puts its top at the top, so a
        // hit on the section's last row would land with the row below the fold. An alert's is on its name cell.

        public const string AnchorPosition = "settings.position";
        public const string AnchorDelta = "settings.delta-reference";
        public const string AnchorDeltaPrecision = "settings.delta-precision";
        public const string AnchorSession = "settings.session-progress";
        public const string AnchorDriverNames = "settings.driver-names";
        public const string AnchorTeamNames = "settings.team-names";
        public const string AnchorClock = "settings.clock";
        public const string AnchorUnits = "settings.units";
        public const string AnchorBlueFlag = "settings.blue-flag-detail";
        public const string AnchorFlagsInPitLane = "settings.flags-in-pit-lane";
        public const string AnchorLowFuel = "settings.low-fuel";
        public const string AnchorOilTemp = "settings.oil-temperature";
        public const string AnchorWaterTemp = "settings.water-temperature";
        public const string AnchorBrightness = "settings.brightness";
        public const string AnchorNightBrightness = "settings.night-brightness";
        public const string AnchorNightMode = "settings.night-mode";

        /// <summary>Each section's live rows' anchors, in the order it draws them, so a route to a row marks
        /// its section's link (SectionOf).</summary>
        public static readonly string[][] RowAnchorsBySection =
        {
            new[] { AnchorPosition, AnchorDelta, AnchorDeltaPrecision, AnchorSession, AnchorDriverNames, AnchorTeamNames, AnchorClock, AnchorUnits },
            new[] { AnchorBlueFlag, AnchorFlagsInPitLane },
            new[] { AnchorLowFuel, AnchorOilTemp, AnchorWaterTemp },
            new[] { AnchorBrightness, AnchorNightBrightness, AnchorNightMode },
            new string[0],
            new string[0],
        };

        // --- The "On this page" row -------------------------------------------------------------------

        /// <summary>18 under the title, as the artboard's nav; PageLayout already puts the page's 28 there.</summary>
        public const double IndexTop = 18;

        /// <summary>14 over the rule that closes the row.</summary>
        public const double IndexPaddingBottom = 14;

        public const double IndexGap = 4;
        public const double IndexLinkHeight = 30;
        public const double IndexLinkPaddingX = 12;
        public const double IndexLinkTextSize = 14;

        /// <summary>The .idx's 500, Medium.</summary>
        public const int IndexLinkFontWeight = 500;

        /// <summary>How far under the top of the view a section's heading may be and still count as the one
        /// being read: the page's own top padding.</summary>
        public const double IndexReadLine = 36;

        /// <summary>How far under the top of the view a link's press puts its section's heading.</summary>
        public const double IndexJumpMargin = 20;

        /// <summary>
        /// The section being read: the one a link just sent the view to while its heading is still in view;
        /// otherwise the last once the view can scroll no further, since a short last section never reaches
        /// the top; otherwise the last whose top has reached <paramref name="readLine"/>, and the first when
        /// none has.
        /// </summary>
        /// <param name="tops">Each section's top, measured from the top of the view.</param>
        /// <param name="held">The section a link's press sent the view to, or -1.</param>
        public static int CurrentSection(IList<double> tops, double readLine, double viewHeight, bool atEnd, int held)
        {
            if (tops == null || tops.Count == 0) return 0;
            if (held >= 0 && held < tops.Count && tops[held] >= -readLine && tops[held] < viewHeight) return held;
            if (atEnd) return tops.Count - 1;
            var current = 0;
            for (var i = 0; i < tops.Count; i++)
            {
                if (tops[i] <= readLine) current = i;
            }
            return current;
        }

        /// <summary>The mark a build of the row starts with: the last build's on a rebuild in place, which keeps
        /// the scroll where the driver was; on arriving (<paramref name="kept"/> is -1), the section the route
        /// lands in.</summary>
        public static int IndexStartMark(int kept, string anchor)
        {
            return kept >= 0 ? kept : SectionOf(anchor);
        }

        /// <summary>The section a build holds from the start: the last build's hold on a rebuild in place; on
        /// arriving at an anchor, the section it lands in, so the landing scroll, which leaves a short section
        /// at the foot of the view, cannot mark the one above it; nothing on arriving at the page's top.</summary>
        public static int IndexStartHeld(int keptMark, int keptHeld, string anchor)
        {
            if (keptMark >= 0) return keptHeld;
            return anchor == null ? -1 : SectionOf(anchor);
        }

        /// <summary>The section a route's anchor lands in: a section's own anchor, or the section that draws
        /// a live row or a greyed row when it is one of this page's. 0 for anything else.</summary>
        public static int SectionOf(string anchor)
        {
            if (anchor == null) return 0;
            var index = Array.IndexOf(SectionAnchors, anchor);
            if (index >= 0) return index;
            for (var s = 0; s < RowAnchorsBySection.Length; s++)
            {
                if (Array.IndexOf(RowAnchorsBySection[s], anchor) >= 0) return s;
            }
            for (var s = 0; s < SoonBySection.Length; s++)
            {
                if (SoonBySection[s].Any(item => item.Anchor == anchor)) return s;
            }
            return 0;
        }

        // --- Rows: stacking ---------------------------------------------------------------------------

        /// <summary>Below this much content a row's control goes under its title rather than beside it. The
        /// widest row is the greyed Colour vision: its four options beside its title and Soon tag need about
        /// 510, measured with Barlow's advances, so 560 clears it by about 50. Next are Night brightness at
        /// about 455, its title beside the sliders' fixed 320, and the driver names' four examples at about
        /// 430, then Theme, Colours and Name at about 420 and Brightness at about 415: past Colour vision, a
        /// wider slider is the next thing to push a row over the threshold.</summary>
        public const double StackControlsBelow = 560;

        /// <summary>Between a title and the control stacked under it.</summary>
        public const double StackedControlGap = 10;

        /// <summary>Between a title and the New or Soon tag after it: the artboard's .t gap.</summary>
        public const double TitleTagGap = 8;

        public static bool StacksControls(double contentWidth)
        {
            return contentWidth < StackControlsBelow;
        }

        // --- Race data ---------------------------------------------------------------------------------

        public const string UnitsTitle = "Units";

        /// <summary>Where to change them, which nothing on the row can say.</summary>
        public const string UnitsCaption = "Set in SimHub.";

        public const double UnitsTextSize = 14;

        /// <summary>The Units line in the regular 400, as the artboard's row value.</summary>
        public const int UnitsFontWeight = 400;

        /// <summary>The .num-in's 600, SemiBold, which a placeholder over it takes too.</summary>
        public const int NumberFieldFontWeight = 600;

        /// <summary>How far inside a text box's padding its placeholder sits: WPF draws a TextBox's text 2 in
        /// from its padding, so the placeholder is set in as far to sit where typed text would.</summary>
        public const double HintInset = 2;

        /// <summary>The greyed fuel target's box and its unit, 8 apart as the artboard's row.</summary>
        public const double FuelTargetGap = 8;

        /// <summary>The greyed tyre display's two buttons, 6 apart as the artboard's.</summary>
        public const double TyreButtonGap = 6;

        /// <summary>The artboard's dash in a greyed box with nothing in it yet -- the fuel target, Pit window
        /// open and Hybrid battery low -- drawn rather than typed: the em dash of the field's Barlow Condensed
        /// SemiBold at 15 px, whose ink is 8.1 by 1.5.</summary>
        public const double NoValueDashWidth = 8;
        public const double NoValueDashWeight = 1.5;

        /// <summary>The fuel target's greyed row (#326) shows the fuel unit; litres when SimHub cannot say.</summary>
        public const string FuelTargetUnitFallback = "L";

        /// <summary>
        /// The main and the secondary value a tyre corner will show (#325), greyed until then, in that order.
        /// Two names: the artboard's "then Pressure" is a fragment in a chooser, which voice.md does not allow
        /// and which beats artboard copy. The row is drawn bare, as the artboard draws it: a greyed row has
        /// nothing to act on, so a caption explaining the order would be words for nothing, and #325, which
        /// builds the two choosers, settles their words.
        /// </summary>
        public static readonly string[] TyreDisplayLabels = { "Temperature", "Pressure" };

        public static string SpeedUnit(string simHub)
        {
            switch (simHub)
            {
                case "Kmh": return "km/h";
                case "Mph": return "mph";
                default: return null;
            }
        }

        /// <summary>SimHub's TemperatureUnit, spelled "Celcius" as SimHub spells it.</summary>
        public static string TemperatureUnit(string simHub)
        {
            switch (simHub)
            {
                case "Celcius": return "°C";
                case "Fahrenheit": return "°F";
                case "Kelvin": return "K";
                default: return null;
            }
        }

        public static string PressureUnit(string simHub)
        {
            switch (simHub)
            {
                case "Psi": return "psi";
                case "Kpa": return "kPa";
                case "Bar": return "bar";
                default: return null;
            }
        }

        public static string FuelUnit(string simHub)
        {
            switch (simHub)
            {
                case "Liters": return "L";
                case "Gallons": return "gal";
                default: return null;
            }
        }

        /// <summary>The unit after the greyed fuel target's box: SimHub's, or litres when it cannot say.</summary>
        public static string FuelTargetUnit(string simHub)
        {
            return FuelUnit(simHub) ?? FuelTargetUnitFallback;
        }

        /// <summary>
        /// The units row's line, "km/h · °C · bar · L", from SimHub's four enum names, in the artboard's order;
        /// the ones SimHub did not answer are left out, and null when it answered none.
        /// </summary>
        public static string UnitsLine(string speed, string temperature, string pressure, string fuel)
        {
            var parts = new[] { SpeedUnit(speed), TemperatureUnit(temperature), PressureUnit(pressure), FuelUnit(fuel) }
                .Where(part => part != null)
                .ToArray();
            return parts.Length == 0 ? null : string.Join(" · ", parts);
        }

        /// <summary>Whether SimHub's units are not the ones the page was drawn with, so the texts they decide
        /// are set again in place: any of the four moved, including one SimHub could not say before and can now.</summary>
        public static bool UnitsMoved(IList<string> drawn, IList<string> now)
        {
            if (drawn == null || now == null) return !ReferenceEquals(drawn, now);
            return !drawn.SequenceEqual(now, StringComparer.Ordinal);
        }

        // --- Flags -------------------------------------------------------------------------------------

        public const string FlagsInPitLaneTitle = "Flags in the pit lane";

        /// <summary>What #504 will offer, greyed until then: whether a yellow is shown for the whole track or
        /// only for the driver's own sector. The artboard's words, which #504 names too: rewording them is
        /// one of its acceptance items, so the row keeps them until it settles them.</summary>
        public static readonly string[] YellowFlagLabels = { "Whole track", "My sector only" };

        // --- Alerts ------------------------------------------------------------------------------------

        public const string LowFuelTitle = "Low fuel";
        public const string OilTempTitle = "Oil temperature";
        public const string WaterTempTitle = "Water temperature";

        /// <summary>Under both temperatures: what the number is in, which is SimHub's unit and not OpenDash's
        /// (ruling 66), so a Fahrenheit rig types Fahrenheit. The default is the box's placeholder.</summary>
        public const string TemperatureCaption = "In SimHub's unit.";

        public const string AlertColumn = "Alert";

        /// <summary>What sets each row off. The artboard heads the column "When", an adverb; voice.md makes a
        /// heading a noun, and voice.md beats artboard copy. The noun is "Trigger" (#524, ruling 7), which also
        /// covers Pit window open, an event whose row draws an empty box and has no threshold; the Matrix page's
        /// Warnings row links here as its plural (PanelMatrix.ThresholdsLink), and search still finds the table by
        /// "threshold". A departure from the artboard that plugin.md records.</summary>
        public const string ThresholdColumn = "Trigger";

        /// <summary>The four columns #512 will make answer, greyed until then.</summary>
        public static readonly string[] SurfaceColumns = { "Screens", "LEDs", "Matrix", "Races only" };

        public const string TryLabel = "Try";

        public const string Under = "under";
        public const string Over = "over";
        public const string At = "at";

        /// <summary>Low fuel's unit as the table holds it; drawn as <see cref="LapsUnitFor"/> says for the
        /// number in the box beside it.</summary>
        public const string LapsUnit = "laps";
        public const string LapUnit = "lap";

        /// <summary>"under [1] lap", "under [2] laps": the unit follows the number (#524, ruling 11), and so
        /// does an empty or unreadable box, which reads as more than one.</summary>
        public static string LapsUnitFor(int laps)
        {
            return laps == 1 ? LapUnit : LapsUnit;
        }

        /// <summary>The unit for what the box holds as typed, before it is committed.</summary>
        public static string LapsUnitFor(string typed)
        {
            int laps;
            return int.TryParse((typed ?? string.Empty).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out laps)
                ? LapsUnitFor(laps)
                : LapsUnit;
        }

        public const int LowFuelMax = 99;
        public const int TemperatureMax = 999;

        /// <summary>
        /// The table's rows, in the artboard's order: the three the rig warns at today, then the four that
        /// are coming.
        /// </summary>
        /// <remarks>
        /// A tick on a live row reads as a fact, so the live rows' columns are where each shows today, as #512
        /// sets them out, and not the artboard's mock ticks. Low fuel is on every surface: the screens' fuel
        /// card and band D telltale, the strips' car lamp and fuel centre (LightsLowFuelLaps is the number
        /// this row writes, and the strip effect is on by default), and the matrix's warning glyph. Oil and
        /// water are the matrix's alone: no screen reads either threshold, and the screens' engine telltale
        /// lights from the sim's own warning bits. All of them warn in every session. The greyed rows'
        /// columns are the artboard's, which is what #512 is to decide.
        /// </remarks>
        public static readonly IReadOnlyList<SettingsAlert> Alerts = new[]
        {
            new SettingsAlert(LowFuelTitle, Under, LapsUnit, null, true, true, true, false, PanelEmulation.LowFuel, AnchorLowFuel),
            new SettingsAlert(OilTempTitle, Over, null, null, false, false, true, false, PanelEmulation.Oil, AnchorOilTemp),
            new SettingsAlert(WaterTempTitle, Over, null, null, false, false, true, false, PanelEmulation.Water, AnchorWaterTemp),
            new SettingsAlert(PanelSoon.TyreWear.Title, Over, string.Empty, "70%", true, false, false, true, null),
            new SettingsAlert(PanelSoon.PitWindowOpen.Title, string.Empty, string.Empty, null, true, true, true, true, null),
            new SettingsAlert(PanelSoon.Incidents.Title, At, "x", "12", true, false, false, true, null),
            new SettingsAlert(PanelSoon.HybridBatteryLow.Title, Under, "V", null, true, false, true, false, null),
        };

        /// <summary>The row of that title, or null.</summary>
        public static SettingsAlert Alert(string title)
        {
            return Alerts.FirstOrDefault(alert => string.Equals(alert.Title, title, StringComparison.Ordinal));
        }

        /// <summary>
        /// The default a temperature of 0 stands for, in SimHub's unit, which the empty box shows as its
        /// placeholder: 120 °C (248 °F) for oil and 110 °C (230 °F) for water. Null when SimHub did not say.
        /// </summary>
        public static int? TemperatureDefault(bool oil, string simHubUnit)
        {
            int value;
            if (simHubUnit == null) return null;
            var table = oil ? Contract.DefaultOilTemp : Contract.DefaultWaterTemp;
            return table.TryGetValue(simHubUnit, out value) ? value : (int?)null;
        }

        /// <summary>What a threshold box holds: nothing for 0, which is the unit's default and shows as the
        /// placeholder, and the number otherwise.</summary>
        public static string ThresholdText(int? value)
        {
            return value.HasValue && value.Value > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        /// <summary>What a threshold box's text commits: 0 (the default) for an empty box, the last value for
        /// something that is not a number, and a number held to 0..max.</summary>
        public static int ParseThreshold(string text, int last, int max)
        {
            var trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0) return 0;
            int parsed;
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return last;
            return parsed < 0 ? 0 : parsed > max ? max : parsed;
        }

        /// <summary>From this much content the table carries the four greyed surface columns; under it they
        /// fold into one greyed "Alert display" row under the table, so the live columns keep their room.</summary>
        public const double AlertSurfacesFrom = 680;

        public static bool AlertSurfacesFit(double contentWidth)
        {
            return contentWidth >= AlertSurfacesFrom;
        }

        /// <summary>The artboard's th width: 30% of the table for the alert's name, where the surface columns are
        /// drawn. The rest spreads over the surface columns, as the artboard's auto layout spreads it, rather
        /// than all of it going to the names.</summary>
        public const double AlertNameShare = 0.3;

        /// <summary>The names' weight against each surface column's 1: enough that the names reach their 30%
        /// wherever the surface columns have their room.</summary>
        public const double AlertNameWeight = 3;

        /// <summary>The longest live name, "Water temperature", with its cell's padding and some to spare.</summary>
        public const double AlertNameMinWidth = 150;

        /// <summary>The most the names' column takes of a table that wide.</summary>
        public static double AlertNameMaxWidth(double tableWidth)
        {
            return Math.Max(AlertNameMinWidth, tableWidth * AlertNameShare);
        }

        public const double AlertCellPaddingX = 12;
        public const double AlertCellPaddingY = 10;
        public const double AlertTextSize = 14;

        /// <summary>The name cell's 500, Medium.</summary>
        public const int AlertNameFontWeight = 500;

        /// <summary>The Alerts heading and its New tag, 10 apart as the artboard's h2.</summary>
        public const double AlertsHeadingTagGap = 10;

        /// <summary>Over the "Alert display" row the table folds into, under the card: the build's own, since
        /// the artboard never folds.</summary>
        public const double AlertFoldGap = 8;
        public const double AlertThresholdGap = 8;
        public const double AlertCheck = 16;
        public const double AlertCheckIcon = 12;
        public const double AlertTryTextSize = 13;
        public const double AlertHeaderTagGap = 4;

        // --- Lighting ----------------------------------------------------------------------------------

        public const string BrightnessTitle = "Brightness";
        /// <summary>The one thing the row cannot say. The artboard draws both sliders bare, and the night one
        /// stays bare: "used while night mode is on" would only say its label again (docs/design/voice.md).</summary>
        public const string BrightnessCaption = "SimHub's device brightness applies on top.";
        public const string NightBrightnessTitle = "Night brightness";
        public const string NightModeTitle = "Night mode";

        /// <summary>The row whose chip opens the night-mode binding on Shortcuts. Not a search label: the
        /// binding lives on Shortcuts, where "night mode button" finds it.</summary>
        public const string NightModeButtonTitle = "Night mode button";

        /// <summary>The scenario chips' section, in the Rig page's one noun for it (#524, ruling 6).</summary>
        public const string PreviewTitle = PanelRigMap.ScenariosName;

        public const string PreviewDay = "day";
        public const string PreviewNight = "night";
        public static readonly string[] PreviewValues = { PreviewDay, PreviewNight };
        public static readonly string[] PreviewLabels = { "Day", "Night" };

        /// <summary>Whether the preview shows night: the driver's pick on the preview, which writes nothing,
        /// or night mode itself until they pick.</summary>
        public static bool ShowsNight(bool? picked, bool nightMode)
        {
            return picked ?? nightMode;
        }

        /// <summary>The driver's pick while it stands: one made under a night mode is let go of once night mode
        /// moves, whether by the page's switch, the sidebar's or the wheel's button, so every change of night
        /// mode hands the preview back to it.</summary>
        public static bool? KeptPick(bool? picked, bool pickedUnder, bool nightMode)
        {
            return picked.HasValue && pickedUnder == nightMode ? picked : null;
        }

        /// <summary>How bright the preview draws the lights: the day brightness by day and the night
        /// brightness by night.</summary>
        public static double PreviewLevel(bool night, int dayBrightness, int nightBrightness)
        {
            return PanelEmulation.Dim(night, dayBrightness, nightBrightness);
        }

        /// <summary>The preview's numeral: "100%" by day at full brightness, "25%" at night by default.</summary>
        public static string PreviewPercent(bool night, int dayBrightness, int nightBrightness)
        {
            var percent = (int)Math.Round(PreviewLevel(night, dayBrightness, nightBrightness) * 100);
            return percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>The preview's strip: 3·9·3, a yellow on both ends, so the ends and the revs both show the
        /// dim.</summary>
        public const int PreviewEnds = 3;
        public const string PreviewStripScenario = PanelEmulation.Yellow;

        /// <summary>The preview's centre, the artboard's: the revs near the top, three green, three amber, one
        /// red and two unlit. Its own frame rather than a scenario's, whose revs stop short of the red.</summary>
        public static string[] PreviewCentre()
        {
            return new[] { Theme.ShiftStage1, Theme.ShiftStage1, Theme.ShiftStage1, Theme.ShiftStage2, Theme.ShiftStage2, Theme.ShiftStage2, Theme.ShiftStage3, null, null };
        }

        /// <summary>The preview's strip: the yellow's ends from PanelEmulation, around the artboard's centre.</summary>
        public static string[][] PreviewStripFrame()
        {
            var centre = PreviewCentre();
            var frame = PanelEmulation.StripFrame(PreviewEnds, centre.Length, PreviewStripScenario);
            frame[1] = centre;
            return frame;
        }

        /// <summary>The preview's matrix: the gear, at the revs a car is driven at.</summary>
        public const string PreviewMatrixScenario = PanelEmulation.Mid;

        public static MatrixOptions PreviewMatrixOptions()
        {
            return new MatrixOptions { Rest = "gear" };
        }

        /// <summary>The artboard's 20 px LEDs, 3 apart and the groups 7 apart, with no ground of their own.</summary>
        public static readonly StripStyle PreviewStrip = new StripStyle(20, 3, 3, 7, 0, 0, null, null, 0, Theme.SurfaceRaised);

        /// <summary>The artboard's 9 px cells, 2 apart.</summary>
        public static readonly MatrixStyle PreviewMatrix = new MatrixStyle(9, 2, 0, null, null, 0, Theme.SurfaceZone);

        public const double PreviewPaddingX = 18;
        public const double PreviewPaddingY = 16;
        public const double PreviewGap = 16;
        public const double PreviewMarginBottom = 8;
        public const double PreviewStagePadding = 18;
        public const double PreviewStageGap = 40;

        public const double PreviewPercentSize = 22;

        /// <summary>The .num's 600, SemiBold.</summary>
        public const int PreviewPercentFontWeight = 600;
        public const double PreviewPercentWidth = 56;

        /// <summary>The brightness sliders' width, with their numeral, as the artboard's.</summary>
        public const double SliderWidth = 320;

        /// <summary>The artboard's 320, or the whole content where there is less.</summary>
        public static double SliderWidthFor(double contentWidth)
        {
            return Math.Max(0, Math.Min(SliderWidth, contentWidth));
        }

        // --- Appearance (greyed) -----------------------------------------------------------------------

        public static readonly string[] ThemeLabels = { "Dark", "Light", "OLED black", "High contrast" };
        public static readonly string[] ColourVisionLabels = { "Standard", "Deuteranopia", "Protanopia", "Tritanopia" };
        public static readonly string[] ColoursLabels = { "As designed", "Darker", "Lighter", "Warmer" };

        // --- Driver (greyed) ---------------------------------------------------------------------------

        public const string FirstNameHint = "First name";
        public const string SurnameHint = "Surname";
        public const string RaceNumberHint = "000";
        public const string LogoButton = "Choose PNG or JPEG…";
        public const string IdleBackgroundButton = "Choose image…";
        public const double NameInputWidth = 150;
        public const double DriverInputGap = 8;

        // --- Greyed rows -------------------------------------------------------------------------------

        /// <summary>Each section's greyed rows, in the order it draws them.</summary>
        public static readonly SoonItem[][] SoonBySection =
        {
            new[] { PanelSoon.FuelTargetPerLap, PanelSoon.TyreDisplay },
            new[] { PanelSoon.YellowFlags },
            new[] { PanelSoon.TyreWear, PanelSoon.PitWindowOpen, PanelSoon.Incidents, PanelSoon.HybridBatteryLow, PanelSoon.AlertDisplay },
            new[] { PanelSoon.SimTimeOfDay, PanelSoon.ScreenDimming },
            new[] { PanelSoon.DashTheme, PanelSoon.ColourVision, PanelSoon.Colours },
            new[] { PanelSoon.BrandName, PanelSoon.BrandRaceNumber, PanelSoon.BrandLogo, PanelSoon.IdleScreenBackground },
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = SoonBySection.SelectMany(section => section).ToArray();

        // --- Search ------------------------------------------------------------------------------------

        /// <summary>Every section heading, routed to its section, and every live row label, routed to its own
        /// row. The greyed rows are listed by PanelSearch.Soon from SoonDrawn, and the night-mode button's row
        /// by Shortcuts.</summary>
        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(PanelDataTab.SectionTitle, PanelPage.Settings, AnchorRaceData),
            new PanelSearch.Entry(PanelDataTab.PositionTitle, PanelPage.Settings, AnchorPosition, "overall", "class"),
            new PanelSearch.Entry(PanelDataTab.DeltaTitle, PanelPage.Settings, AnchorDelta, "session best", "all-time best", "last lap"),
            new PanelSearch.Entry(PanelDataTab.DeltaPrecisionTitle, PanelPage.Settings, AnchorDeltaPrecision, "hundredths", "thousandths", "decimals"),
            new PanelSearch.Entry(PanelDataTab.SessionTitle, PanelPage.Settings, AnchorSession, "laps", "time"),
            new PanelSearch.Entry(PanelDataTab.DriverNameTitle, PanelPage.Settings, AnchorDriverNames, "name format", "surname"),
            new PanelSearch.Entry(PanelDataTab.TeamNameTitle, PanelPage.Settings, AnchorTeamNames, "team"),
            new PanelSearch.Entry(PanelDataTab.ClockTitle, PanelPage.Settings, AnchorClock, "24h", "12h", "time of day"),
            new PanelSearch.Entry(UnitsTitle, PanelPage.Settings, AnchorUnits, "km/h", "mph", "celsius", "fahrenheit", "litres", "gallons"),
            new PanelSearch.Entry(FlagsTitle, PanelPage.Settings, AnchorFlags),
            new PanelSearch.Entry(PanelDataTab.BlueFlagTitle, PanelPage.Settings, AnchorBlueFlag, "blue flag"),
            new PanelSearch.Entry(FlagsInPitLaneTitle, PanelPage.Settings, AnchorFlagsInPitLane, "pit", "band d"),
            new PanelSearch.Entry(AlertsTitle, PanelPage.Settings, AnchorAlerts, "warning", "threshold", "trigger"),
            new PanelSearch.Entry(LowFuelTitle, PanelPage.Settings, AnchorLowFuel, "warning", "laps", "fuel"),
            new PanelSearch.Entry(OilTempTitle, PanelPage.Settings, AnchorOilTemp, "warning", "threshold", "hot"),
            new PanelSearch.Entry(WaterTempTitle, PanelPage.Settings, AnchorWaterTemp, "warning", "threshold", "coolant", "hot"),
            new PanelSearch.Entry(LightingTitle, PanelPage.Settings, AnchorLighting, "lights", "preview"),
            new PanelSearch.Entry(BrightnessTitle, PanelPage.Settings, AnchorBrightness, "lights", "leds", "dim"),
            new PanelSearch.Entry(NightBrightnessTitle, PanelPage.Settings, AnchorNightBrightness, "night", "dim"),
            new PanelSearch.Entry(NightModeTitle, PanelPage.Settings, AnchorNightMode, "dark", "dim"),
            new PanelSearch.Entry(AppearanceTitle, PanelPage.Settings, AnchorAppearance, "look", "theme", "colours"),
            new PanelSearch.Entry(DriverTitle, PanelPage.Settings, AnchorDriver, "branding", "logo", "race number"),
        };

        /// <summary>Search labels this page draws through something other than the constant: none.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>();
    }
}
