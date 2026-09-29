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
            var delta = BuildSegmented(Contract.DeltaReferences, new[] { "Session best", "All-time best" }, Settings.DeltaReference, value =>
            {
                Settings.DeltaReference = value;
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

            // A gap of this tab's own rather than the section default: PanelDataTab.RowGap says why, and
            // passing it here is what keeps Install and Lights on the twenty they are drawn at.
            return Ui.VStack(0, Ui.Section(PanelDataTab.SectionTitle, PanelDataTab.RowGap,
                Ui.Row(PanelDataTab.PositionTitle, PanelDataTab.PositionCaption, position),
                Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta),
                Ui.Row(PanelDataTab.SessionTitle, PanelDataTab.SessionCaption, session),
                Ui.Row(PanelDataTab.BlueFlagTitle, PanelDataTab.BlueFlagCaption, blueFlag),
                Ui.Row(PanelDataTab.DriverNameTitle, PanelDataTab.DriverNameCaption, driverName),
                Ui.Row(PanelDataTab.TeamNameTitle, PanelDataTab.TeamNameCaption, teamName),
                Ui.Row(PanelDataTab.ClockTitle, PanelDataTab.ClockCaption, clock)));
        }
    }
}
