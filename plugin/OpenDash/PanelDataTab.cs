// PanelDataTab.cs: the words the Data tab puts beside each of its controls, and the gap it sets them
// at.
//
// Apart from SettingsControl.Data.cs for the reason PanelLights.cs is apart from
// SettingsControl.Lights.cs: the tab is WPF and the net8.0 test project cannot compile a line of it, so
// copy a test can hold has to live where it can reach. Some rows here say something the canvas does not,
// on purpose -- the Position row's sentences, the delta reference's third segment -- and a constant with
// a test on it is the only way that stays a decision rather than a drift.
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
        /// The tab is short, so the column under the label reads as one block rather than as a handful
        /// of separate settings unless they are pushed apart; the design audit takes the 22 off the
        /// canvas. It is this tab's own number rather than PanelMetrics.SectionGap
        /// because Install and Lights are long and lengthening them further buys nothing. 22 is off
        /// design/tokens.json's space scale, which steps 16 to 24, so it is recorded here rather than
        /// rounded to a token that would say something else.
        /// </remarks>
        public const double RowGap = 22;

        /// <summary>The rev bar's words, which now belong to a screen's own pane rather than to this
        /// tab. They stay here because this file is where the panel's copy a test can hold lives, and
        /// because the row read exactly the same when it was rig-wide -- what changed is who it answers
        /// for, not what it says.</summary>
        public const string RevBarTitle = "Revbar";

        /// <summary>
        /// Two answers, not three.
        /// </summary>
        /// <remarks>
        /// "Shift lights" and "RPM bar" were offered as if they were tastes, and they are not: the bar
        /// has one right behaviour and it is the car's own. Where OpenDash has a table for the car it
        /// draws that car's lights ([ADR 0018](docs/decisions/0018-the-cars-own-lights.md)); where it
        /// has none it draws SimHub's bands, which is an RPM bar that ends in shift lights, and that is
        /// what almost every car does. A driver asked to choose between them was being asked a question
        /// OpenDash should answer, and one of the two answers was worse.
        ///
        /// So the row is on or off, and off is the one that still means something: a wheel with its own
        /// LEDs does not need the strip, and the room goes back to the zones.
        /// </remarks>
        public const string RevBarCaption = "Turn off if your wheel has its own shift lights.";

        public static readonly string[] RevBarValues = { Contract.RevBarShift, Contract.RevBarOff };

        public static readonly string[] RevBarLabels = { "On", "Off" };

        public const string PositionTitle = "Position";

        /// <summary>Two sentences the canvas does not carry, neither of which the control can show.</summary>
        /// <remarks>
        /// The row used to say that each zone could override this, which is the one thing a zone cannot
        /// do. Class stopped being a readout in #212: it decides who is in a list as well as how a
        /// position is numbered, and a zone's own filter is joined to it rather than set against it, so
        /// a zone filters a list the rig left on Overall and never the other way about. Both halves are
        /// therefore owed to the canvas rather than deleted from the code: the first is a shipped
        /// behaviour a driver chooses rather than discovers, and the second is the zone filter that
        /// SettingsControl.Panes.cs draws and FaceSettings.SetClassOnly stores.
        /// </remarks>
        public const string PositionCaption = "Class also shows only your own class in lists. A zone can ask for that on its own.";

        public const string DeltaTitle = "Delta reference";

        public const string DeltaCaption = "Which lap the delta compares against.";

        /// <summary>One label per reference, in the contract's order.</summary>
        /// <remarks>
        /// "Last lap" is the canvas's own name for that lap, the one the Last lap card and the Lap times
        /// page draw, so the row names a lap the driver has already seen a time for rather than
        /// inventing a word for it. The canvas draws this row with the first two segments only; the
        /// third is one it does not carry yet (#322).
        /// </remarks>
        public static readonly string[] DeltaLabels = { "Session best", "All-time best", "Last lap" };

        public const string SessionTitle = "Session progress";

        public const string SessionCaption = "Auto picks laps or time to suit the session.";

        public const string BlueFlagTitle = "Blue flag detail";

        public const string BlueFlagCaption = "What shows next to a blue flag.";

        public const string DriverNameTitle = "Driver names";

        /// <summary>No caption: the four values are worked examples of one name, so the control says
        /// exactly what each of them does and a sentence under it would restate it.</summary>
        public const string DriverNameCaption = null;

        /// <summary>The four formats, shown as what they make of one name rather than described.
        ///
        /// A value in a chooser is read without its label and alongside the values beside it
        /// (docs/design/voice.md), and "Initial and surname" beside "Surname and initial" is two
        /// fragments a reader has to decode; `L. Byrne` beside `B. Liam` is the answer itself. The name
        /// is the one the design canvas and the emulator's own field both use.</summary>
        public static readonly string[] DriverNameLabels = { "Liam Byrne", "L. Byrne", "B. Liam", "Byrne Liam" };

        public const string TeamNameTitle = "Team names";

        /// <summary>Two facts the control cannot show: which of the two names it swaps, and that a car
        /// with no team keeps its driver rather than going blank. The second is the one worth the line:
        /// without it a half-filled column reads as a fault.</summary>
        public const string TeamNameCaption = "Names the team instead of the driver, and keeps the driver where the sim has no team.";
    }
}
