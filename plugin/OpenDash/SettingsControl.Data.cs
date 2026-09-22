// SettingsControl.Data.cs: the Data tab -- the settings that mean the same thing on every screen.
//
// A lap time compares against the same lap on the rim as it does on the pit wall, so these are not per
// screen and are not on a screen's pane. They are the whole of the tab, which is the point: it is short
// because almost nothing genuinely is global, and the rev bar left it for each face's own pane when it
// turned out not to be -- a rim that already carries LEDs across its top and a display that does not
// are two screens on one rig, and one switch was answering for both.
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

            // A gap of this tab's own rather than the section default: PanelDataTab.RowGap says why, and
            // passing it here is what keeps Install and Lights on the twenty they are drawn at.
            return Ui.VStack(0, Ui.Section(PanelDataTab.SectionTitle, PanelDataTab.RowGap,
                Ui.Row(PanelDataTab.PositionTitle, PanelDataTab.PositionCaption, position),
                Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta),
                Ui.Row(PanelDataTab.SessionTitle, PanelDataTab.SessionCaption, session),
                Ui.Row(PanelDataTab.BlueFlagTitle, PanelDataTab.BlueFlagCaption, blueFlag)));
        }
    }
}
