// PanelDataTab.cs: the words the Data tab puts beside each of its four controls, and the gap it sets
// them at.
//
// Apart from SettingsControl.Data.cs for the reason PanelLights.cs is apart from
// SettingsControl.Lights.cs: the tab is WPF and the net8.0 test project cannot compile a line of it, so
// copy a test can hold has to live where it can reach. One row here says something the canvas does not,
// on purpose, and a constant with a test on it is the only way that stays a decision rather than a drift.
// Pure: no WPF types.
namespace OpenDashPlugin
{
    public static class PanelDataTab
    {
        public const string SectionTitle = "These apply to every screen";

        public const string SectionCaption = "A lap time means the same thing on the rim as it does on the pit wall, so these are not per screen.";

        /// <summary>Between one setting and the next on this tab, which is wider than the twenty every
        /// other section on the panel is given.</summary>
        /// <remarks>
        /// Four rows are the whole of the tab, so the column under the label is short and reads as one
        /// block rather than as four separate settings unless they are pushed apart; the design audit
        /// takes the 22 off the canvas. It is this tab's own number rather than PanelMetrics.SectionGap
        /// because Install and Lights are long and lengthening them further buys nothing. 22 is off
        /// design/tokens.json's space scale, which steps 16 to 24, so it is recorded here rather than
        /// rounded to a token that would say something else.
        /// </remarks>
        public const double RowGap = 22;

        /// <summary>The rev bar's words, which now belong to a screen's own pane rather than to this
        /// tab. They stay here because this file is where the panel's copy a test can hold lives, and
        /// because the row read exactly the same when it was rig-wide -- what changed is who it answers
        /// for, not what it says.</summary>
        public const string RevBarTitle = "The rev bar";

        public const string RevBarCaption = "Shift lights, a plain RPM bar, or off entirely if this screen's wheel has LEDs of its own. Off gives its room back to the zones.";

        public static readonly string[] RevBarLabels = { "Shift lights", "RPM bar", "Off" };

        public const string PositionTitle = "Position";

        /// <summary>The canvas's sentence, and a second one it does not carry.</summary>
        /// <remarks>
        /// The second sentence is true: every zone of a face has a class filter of its own, which
        /// SettingsControl.Panes.cs draws and FaceSettings.SetClassOnly stores, and without the sentence
        /// this row reads as though Overall meant overall everywhere. It is therefore owed to the canvas
        /// rather than deleted from the code, and the docs package raises it.
        /// </remarks>
        public const string PositionCaption = "Overall, or within your class. A zone can still be set to list your own class on its own.";

        public const string DeltaTitle = "Delta reference";

        public const string DeltaCaption = "Which lap the delta compares against.";

        public const string SessionTitle = "Session progress";

        public const string SessionCaption = "Auto shows laps when the session declares a lap count, time otherwise.";

        public const string BlueFlagTitle = "Blue flag detail";

        public const string BlueFlagCaption = "What the band says beside the blue: nothing, the class of the car behind, or its position and class.";
    }
}
