// SettingsControl.Data.cs: the Data tab -- the settings that mean the same thing on every screen.
//
// A lap time compares against the same lap on the rim as it does on the pit wall, so these are not per
// screen and are not on a screen's pane. They are the whole of the tab, which is the point: it is short
// because almost nothing genuinely is global, and the rev bar left it for each face's own pane when it
// turned out not to be -- a rim that already carries LEDs across its top and a display that does not
// are two screens on one rig, and one switch was answering for both.
//
// How a driver is named passed the same test in the other direction: a name is read by a person, and the
// person does not change between the wheel and the pit wall.
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildDataTab()
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
            // A rig setting rather than a per-screen one: what the band may say about the car behind is
            // the same answer on the wheel as on the pit wall, which is what this whole tab is for.
            var blueFlag = BuildSegmented(Contract.BlueFlagDetails, new[] { "Nothing", "Class", "Position and class" }, Settings.BlueFlagDetail, value =>
            {
                Settings.BlueFlagDetail = value;
                Save();
            });

            // Rig-wide for the same reason as the rest of the tab: a leaderboard on the rim and a board
            // on the pit wall write the same name, and which of the four formats a driver reads fastest
            // is a fact about the driver. #385.
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

            // Rig-wide for the same reason: a driver reads a clock one way on the rim and on the pit
            // wall, and the sim's time of day the way they read their own. #324.
            var clock = BuildSegmented(Contract.ClockFormats, PanelDataTab.ClockLabels, Settings.ClockFormat, value =>
            {
                Settings.ClockFormat = value;
                Save();
            });

            // Rig-wide since #503: a flag shown in the pit lane is the same answer on every surface, and
            // the two thresholds are facts about the car rather than about which corner a box is in.
            // Through the setters, because Normalise fills every panel's entry from the rig's value on
            // the next save and an index into one panel's array would be undone by it.
            var flagsInPitLane = BuildToggle(Settings.FlagsInPitLane, on =>
            {
                Settings.FlagsInPitLane = on;
                Save();
            });
            var oilTemp = BuildNumberBox(Settings.LightsOilTemp ?? 0, 0, 999, v => { Settings.SetLightsOilTemp(v); Save(); });
            var waterTemp = BuildNumberBox(Settings.LightsWaterTemp ?? 0, 0, 999, v => { Settings.SetLightsWaterTemp(v); Save(); });

            // A gap of this tab's own rather than the section default: PanelDataTab.RowGap says why, and
            // passing it here is what keeps Install and Lights on the twenty they are drawn at.
            return Ui.VStack(0, Ui.Section(PanelDataTab.SectionTitle, PanelDataTab.RowGap,
                Ui.Row(PanelDataTab.PositionTitle, PanelDataTab.PositionCaption, position),
                Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta),
                Ui.Row(PanelDataTab.DeltaPrecisionTitle, PanelDataTab.DeltaPrecisionCaption, deltaPrecision),
                Ui.Row(PanelDataTab.SessionTitle, PanelDataTab.SessionCaption, session),
                Ui.Row(PanelDataTab.BlueFlagTitle, PanelDataTab.BlueFlagCaption, blueFlag),
                Ui.Row(PanelDataTab.DriverNameTitle, PanelDataTab.DriverNameCaption, driverName),
                Ui.Row(PanelDataTab.TeamNameTitle, PanelDataTab.TeamNameCaption, teamName),
                Ui.Row(PanelDataTab.ClockTitle, PanelDataTab.ClockCaption, clock),
                Ui.Row("Flags in the pit lane", null, flagsInPitLane),
                Ui.Row("Oil temperature warning", "In SimHub's unit. 0 uses the default.", oilTemp),
                Ui.Row("Water temperature warning", "In SimHub's unit. 0 uses the default.", waterTemp)));
        }
    }
}
