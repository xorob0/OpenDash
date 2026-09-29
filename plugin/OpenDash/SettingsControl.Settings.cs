// SettingsControl.Settings.cs: the Settings page -- only what is the same everywhere: the race data, the flags,
// the alert thresholds, and the lighting.
//
// A lap time compares against the same lap on the rim as it does on the pit wall, so these are not per screen.
// The page is short because almost nothing genuinely is global: the rev bar, the flag display and the lap
// review left for each screen's own pane when they turned out not to be. The #503 foundation re-hosts the
// Data tab's rows here with the rig-wide lighting and the rows #503 made rig-wide (flags in the pit lane, the
// oil and water thresholds); the Settings page agent owns this file and rebuilds it to Settings.dc.html.
//
// The delta's two rows stay adjacent Ui.Row calls: PanelDataTabTests holds that the precision row sits
// directly under the reference it qualifies (#322).
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildSettingsPage(PanelRoute to)
        {
            return PageLayout(PanelSettings.Title, null,
                Ui.Anchor(BuildRaceDataSection(), PanelSettings.AnchorRaceData),
                Ui.Anchor(BuildFlagsSection(), PanelSettings.AnchorFlags),
                Ui.Anchor(BuildAlertsSection(), PanelSettings.AnchorAlerts),
                Ui.Anchor(BuildLightingSection(), PanelSettings.AnchorLighting));
        }

        private FrameworkElement BuildRaceDataSection()
        {
            var position = BuildSegmented(Contract.PositionModes, new[] { "Overall", "Class" }, Settings.PositionMode, value =>
            {
                Settings.PositionMode = value;
                Save();
            });
            var delta = BuildSegmented(Contract.DeltaReferences, PanelDataTab.DeltaLabels, Settings.DeltaReference, value =>
            {
                Settings.DeltaReference = value;
                Save();
            });
            // Under the reference it qualifies, and rig-wide for the same reason: a delta read to the
            // thousandth on the rim and to the hundredth on the pit wall is two answers. #322.
            var deltaPrecision = BuildSegmented(Contract.DeltaPrecisions, PanelDataTab.DeltaPrecisionLabels, Settings.DeltaPrecision, value =>
            {
                Settings.DeltaPrecision = value;
                Save();
            });
            var session = BuildSegmented(Contract.SessionProgressModes, new[] { "Auto", "Laps", "Time" }, Settings.SessionProgress, value =>
            {
                Settings.SessionProgress = value;
                Save();
            });
            // Rig-wide for the same reason as the rest of the page: a leaderboard on the rim and a board on the
            // pit wall write the same name, and which of the four formats a driver reads fastest is a fact
            // about the driver. #385.
            var driverName = BuildSegmented(Contract.DriverNameFormats, PanelDataTab.DriverNameLabels, Settings.DriverNameFormat, value =>
            {
                Settings.DriverNameFormat = value;
                Save();
            });
            var teamName = BuildToggle(Settings.DriverNameTeam, on =>
            {
                Settings.DriverNameTeam = on;
                Save();
            });
            // A driver reads a clock one way on the rim and on the pit wall, and the sim's time of day the way
            // they read their own. #324.
            var clock = BuildSegmented(Contract.ClockFormats, PanelDataTab.ClockLabels, Settings.ClockFormat, value =>
            {
                Settings.ClockFormat = value;
                Save();
            });

            return PageSection(PanelDataTab.SectionTitle,
                Ui.Row(PanelDataTab.PositionTitle, PanelDataTab.PositionCaption, position),
                Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta),
                Ui.Row(PanelDataTab.DeltaPrecisionTitle, PanelDataTab.DeltaPrecisionCaption, deltaPrecision),
                Ui.Row(PanelDataTab.SessionTitle, PanelDataTab.SessionCaption, session),
                Ui.Row(PanelDataTab.DriverNameTitle, PanelDataTab.DriverNameCaption, driverName),
                Ui.Row(PanelDataTab.TeamNameTitle, PanelDataTab.TeamNameCaption, teamName),
                Ui.Row(PanelDataTab.ClockTitle, PanelDataTab.ClockCaption, clock));
        }

        private FrameworkElement BuildFlagsSection()
        {
            // A rig setting rather than a per-screen one: what the band may say about the car behind is the
            // same answer on the wheel as on the pit wall.
            var blueFlag = BuildSegmented(Contract.BlueFlagDetails, new[] { "Nothing", "Class", "Position and class" }, Settings.BlueFlagDetail, value =>
            {
                Settings.BlueFlagDetail = value;
                Save();
            });
            // Rig-wide since #503: a flag shown in the pit lane is the same answer on every surface.
            var flagsInPitLane = BuildToggle(Settings.FlagsInPitLane, on =>
            {
                Settings.FlagsInPitLane = on;
                Save();
            });
            return PageSection(PanelSettings.FlagsTitle,
                Ui.Row(PanelDataTab.BlueFlagTitle, PanelDataTab.BlueFlagCaption, blueFlag),
                Ui.SettingRow(PanelSettings.FlagsInPitLaneTitle, flagsInPitLane, null, Ui.NewTag()));
        }

        /// <summary>
        /// The thresholds the rig warns at, rig-wide since #503: a threshold is a fact about the car, not
        /// about which corner of the rig a box is in.
        /// </summary>
        /// <remarks>
        /// Through SetLightsOilTemp and SetLightsWaterTemp, never into one matrix's entry: Normalise fills
        /// every panel's entry from the rig's value on the next save, so an index into the per-panel arrays
        /// is an edit the save undoes. Zero is the unit's own default, chosen from SimHub's TemperatureUnit.
        /// </remarks>
        private FrameworkElement BuildAlertsSection()
        {
            var lowFuel = BuildNumberBox(Settings.FlagBoxLowFuelLaps, 0, 99, v => { Settings.FlagBoxLowFuelLaps = v; Save(); });
            var oilTemp = BuildNumberBox(Settings.LightsOilTemp ?? 0, 0, 999, v => { Settings.SetLightsOilTemp(v); Save(); });
            var waterTemp = BuildNumberBox(Settings.LightsWaterTemp ?? 0, 0, 999, v => { Settings.SetLightsWaterTemp(v); Save(); });
            return PageSection(PanelSettings.AlertsTitle,
                Ui.Row(PanelSettings.LowFuelTitle, PanelSettings.LowFuelCaption, lowFuel),
                Ui.Row(PanelSettings.OilTempTitle, PanelSettings.TempCaption, oilTemp),
                Ui.Row(PanelSettings.WaterTempTitle, PanelSettings.TempCaption, waterTemp));
        }

        /// <summary>Brightness and night mode are the rig's rather than any one device's, so they sit here and
        /// in the sidebar rather than inside the first device that happened to want them.</summary>
        private FrameworkElement BuildLightingSection()
        {
            return PageSection(PanelSettings.LightingTitle,
                Ui.Row(PanelSettings.BrightnessTitle, PanelSettings.BrightnessCaption, BuildPercentBox(Settings.LightsBrightness, v => { Settings.LightsBrightness = v; Save(); })),
                Ui.Row(PanelSettings.NightBrightnessTitle, PanelSettings.NightBrightnessCaption, BuildPercentBox(Settings.LightsNightBrightness, v => { Settings.LightsNightBrightness = v; Save(); })),
                Ui.Row(PanelSettings.NightModeTitle, null, BuildToggle(Settings.LightsNightMode, on =>
                {
                    Settings.LightsNightMode = on;
                    Save();
                    RefreshSidebar();
                })));
        }
    }
}
