// PanelSettings.cs: the Settings page's words -- its title and what search finds on it.
//
// Settings holds only what is the same everywhere: the race data, the flags, the alert thresholds and the
// lighting. The race data's words stay in PanelDataTab, where their pins are; the Settings page agent owns
// this file and adds the rest. Pure: no WPF.
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelSettings
    {
        public const string Title = "Settings";

        public const string FlagsTitle = "Flags";
        public const string AlertsTitle = "Alerts";
        public const string LightingTitle = "Lighting";

        public const string FlagsInPitLaneTitle = "Flags in the pit lane";
        public const string LowFuelTitle = "Low fuel";
        public const string LowFuelCaption = "Laps of fuel left.";
        public const string OilTempTitle = "Oil temperature";
        public const string WaterTempTitle = "Water temperature";

        /// <summary>Each threshold's caption names its own default, since "the default" alone pointed at
        /// nothing on the row.</summary>
        public const string OilTempCaption = "0 uses 120 °C (248 °F).";
        public const string WaterTempCaption = "0 uses 110 °C (230 °F).";

        public const string BrightnessTitle = "Brightness";
        public const string BrightnessCaption = "SimHub's device brightness applies on top.";
        public const string NightBrightnessTitle = "Night brightness";
        public const string NightBrightnessCaption = "Used while night mode is on.";
        public const string NightModeTitle = "Night mode";

        public const string AnchorRaceData = "settings.race-data";
        public const string AnchorFlags = "settings.flags";
        public const string AnchorAlerts = "settings.alerts";
        public const string AnchorLighting = "settings.lighting";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(PanelDataTab.SectionTitle, PanelPage.Settings, AnchorRaceData),
            new PanelSearch.Entry(PanelDataTab.PositionTitle, PanelPage.Settings, AnchorRaceData, "overall", "class"),
            new PanelSearch.Entry(PanelDataTab.DeltaTitle, PanelPage.Settings, AnchorRaceData, "session best", "all-time best", "last lap"),
            new PanelSearch.Entry(PanelDataTab.DeltaPrecisionTitle, PanelPage.Settings, AnchorRaceData, "hundredths", "thousandths", "decimals"),
            new PanelSearch.Entry(PanelDataTab.SessionTitle, PanelPage.Settings, AnchorRaceData, "laps", "time"),
            new PanelSearch.Entry(PanelDataTab.DriverNameTitle, PanelPage.Settings, AnchorRaceData, "name format", "surname"),
            new PanelSearch.Entry(PanelDataTab.TeamNameTitle, PanelPage.Settings, AnchorRaceData, "team"),
            new PanelSearch.Entry(PanelDataTab.ClockTitle, PanelPage.Settings, AnchorRaceData, "24h", "12h", "time of day"),
            new PanelSearch.Entry(PanelDataTab.BlueFlagTitle, PanelPage.Settings, AnchorFlags, "blue flag"),
            new PanelSearch.Entry(FlagsInPitLaneTitle, PanelPage.Settings, AnchorFlags, "pit", "band d"),
            new PanelSearch.Entry(LowFuelTitle, PanelPage.Settings, AnchorAlerts, "warning", "laps", "fuel"),
            new PanelSearch.Entry(OilTempTitle, PanelPage.Settings, AnchorAlerts, "warning", "threshold", "hot"),
            new PanelSearch.Entry(WaterTempTitle, PanelPage.Settings, AnchorAlerts, "warning", "threshold", "coolant", "hot"),
            new PanelSearch.Entry(BrightnessTitle, PanelPage.Settings, AnchorLighting, "lights", "leds", "dim"),
            new PanelSearch.Entry(NightBrightnessTitle, PanelPage.Settings, AnchorLighting, "night", "dim"),
            new PanelSearch.Entry(NightModeTitle, PanelPage.Settings, AnchorLighting, "dark", "dim"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>Search labels this page draws through something other than the constant: none.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>();
    }
}
