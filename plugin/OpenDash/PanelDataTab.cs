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

        public const string SectionCaption = null;

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
        public const string RevBarTitle = "Rev bar";

        /// <summary>
        /// Two answers, not three.
        /// </summary>
        /// <remarks>
        /// "Shift lights" and "RPM bar" were offered as if they were tastes, and they are not: the bar
        /// has one right behaviour and it is the car's own. Where openDash has a table for the car it
        /// draws that car's lights ([ADR 0018](docs/decisions/0018-the-cars-own-lights.md)); where it
        /// has none it draws SimHub's bands, which is an RPM bar that ends in shift lights, and that is
        /// what almost every car does. A driver asked to choose between them was being asked a question
        /// openDash should answer, and one of the two answers was worse.
        ///
        /// So the row is on or off, and off is the one that still means something: a wheel with its own
        /// LEDs does not need the strip, and the room goes back to the zones.
        /// </remarks>
        public const string RevBarCaption = "Turn it off if your wheel already has its own shift lights.";

        public static readonly string[] RevBarValues = { Contract.RevBarShift, Contract.RevBarOff };

        public static readonly string[] RevBarLabels = { "On", "Off" };

        public const string PositionTitle = "Position";

        /// <summary>The canvas's sentence, and a second one it does not carry.</summary>
        /// <remarks>
        /// The second sentence is true: every zone of a face has a class filter of its own, which
        /// SettingsControl.Panes.cs draws and FaceSettings.SetClassOnly stores, and without the sentence
        /// this row reads as though Overall meant overall everywhere. It is therefore owed to the canvas
        /// rather than deleted from the code, and the docs package raises it.
        /// </remarks>
        public const string PositionCaption = "Each zone can be set to your class on its own.";

        public const string DeltaTitle = "Delta reference";

        public const string DeltaCaption = "Which lap the delta compares against.";

        public const string SessionTitle = "Session progress";

        public const string SessionCaption = "Auto shows laps in a lapped race and time in a timed one.";

        public const string BlueFlagTitle = "Blue flag detail";

        public const string BlueFlagCaption = "What shows next to a blue flag.";
    }
}
