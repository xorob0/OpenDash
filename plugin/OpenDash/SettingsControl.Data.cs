// SettingsControl.Data.cs: the Data tab -- the four settings that mean the same thing on every screen.
//
// A lap time compares against the same lap on the rim as it does on the pit wall, so these are not per
// screen and are not on a screen's pane. They are the whole of the tab, which is the point: it is short
// because almost nothing genuinely is global.
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildDataTab()
        {
            // Three states in one control rather than a toggle and a second toggle under it: what the
            // top of the face carries is one decision, and a driver whose wheel already has LEDs
            // across it wants the third of them. Off redraws the face without the well, so the zones
            // start where the recess did. XOR-138.
            var revBar = BuildSegmented(Contract.RevBarModes, new[] { "Shift lights", "RPM bar", "Off" }, Settings.RevBarMode(), value =>
            {
                Settings.SetRevBar(value);
                Save();
            });
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

            // A gap of this tab's own rather than the section default: PanelDataTab.RowGap says why, and
            // passing it here is what keeps Install and Lights on the twenty they are drawn at.
            return Ui.VStack(0, Ui.Section(PanelDataTab.SectionTitle, PanelDataTab.RowGap,
                Ui.Caption(PanelDataTab.SectionCaption, BodyWidth),
                Ui.Row(PanelDataTab.RevBarTitle, PanelDataTab.RevBarCaption, revBar),
                Ui.Row(PanelDataTab.PositionTitle, PanelDataTab.PositionCaption, position),
                Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta),
                Ui.Row(PanelDataTab.SessionTitle, PanelDataTab.SessionCaption, session)));
        }
    }
}
