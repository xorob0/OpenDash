// PanelDataTab.cs: the words the race data rows put beside each of their controls, which were the Data
// tab's and are the Settings page's Race data section since #503.
//
// Apart from SettingsControl.Settings.cs for the reason PanelLights.cs is apart from
// SettingsControl.Lights.cs: the page is WPF and the net8.0 test project cannot compile a line of it, so
// copy a test can hold has to live where it can reach. Some rows here say something the Settings artboard
// does not, on purpose -- the Position row's sentences, the captions under the rows it draws bare, and the
// whole of the clock row -- and a constant with a test on it is the only way that stays a decision rather
// than a drift.
// Pure: no WPF types.
namespace OpenDashPlugin
{
    public static class PanelDataTab
    {
        /// <summary>The heading the Settings page draws over these rows, which were the Data tab's: "Race
        /// data", as the Settings artboard names the section (#503). The tab's own 22 px row gap went with
        /// it; a row of the redesign carries its own padding.</summary>
        public const string SectionTitle = "Race data";

        public const string SectionCaption = null;

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

        /// <summary>One label per mode, in the contract's order. "Class" rather than the artboard's "In class":
        /// the caption begins with the word, and a value in a chooser is a name.</summary>
        public static readonly string[] PositionLabels = { "Overall", "Class" };

        public const string DeltaTitle = "Delta reference";

        public const string DeltaCaption = "Which lap the delta compares against.";

        /// <summary>One label per reference, in the contract's order.</summary>
        /// <remarks>
        /// "Last lap" is the canvas's own name for that lap, the one the Last lap card and the Lap times
        /// page draw, so the row names a lap the driver has already seen a time for rather than
        /// inventing a word for it. The Settings artboard draws all three segments (#322).
        /// </remarks>
        public static readonly string[] DeltaLabels = { "Session best", "All-time best", "Last lap" };

        /// <summary>The row under the delta reference, as the Settings artboard draws it (#322).</summary>
        public const string DeltaPrecisionTitle = "Delta precision";

        /// <summary>What each answer is for, which is the one thing the two words cannot say.</summary>
        /// <remarks>
        /// The control already shows the two values, so the caption does not list them; what it adds is
        /// when a driver would want each, a hotlap being decided by the third place and a race being read
        /// in a glance at two.
        /// </remarks>
        public const string DeltaPrecisionCaption = "Thousandths for a hotlap, hundredths to read at a glance.";

        /// <summary>One label per precision, in the contract's order.</summary>
        /// <remarks>
        /// Words, although the driver names and the clock answer their questions with worked examples. A
        /// name is not a numeral, and a delta is nothing else: `0.21` and `0.214` would be set in the
        /// panel's Barlow, where the canvas's fourth rule keeps numerals, version numbers in the plugin
        /// included, to Barlow Condensed. The clock's `14:32` and `2:32 PM` are drawn in that Barlow too.
        /// That is a disagreement between the build and the canvas for the canvas's owner to settle, not
        /// a precedent this row follows.
        /// </remarks>
        public static readonly string[] DeltaPrecisionLabels = { "Hundredths", "Thousandths" };

        public const string SessionTitle = "Session progress";

        public const string SessionCaption = "Auto picks laps or time to suit the session.";

        /// <summary>One label per mode, in the contract's order.</summary>
        public static readonly string[] SessionLabels = { "Auto", "Laps", "Time" };

        public const string BlueFlagTitle = "Blue flag detail";

        public const string BlueFlagCaption = "What shows next to a blue flag.";

        /// <summary>One label per detail, in the contract's order.</summary>
        public static readonly string[] BlueFlagLabels = { "Nothing", "Class", "Position and class" };

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

        public const string ClockTitle = "Clock";

        /// <summary>The one fact the control cannot show: that the sim's time of day follows it too,
        /// which a row labelled "Clock" over two readings of the wall clock does not say. #324.</summary>
        public const string ClockCaption = "The sim's time of day follows it too.";

        /// <summary>The two formats, shown as what they make of one time rather than described.
        ///
        /// For the reason the driver names are examples: a value in a chooser is read without its label
        /// (docs/design/voice.md), and "24-hour" beside "12-hour" asks the reader to picture both, where
        /// `14:32` beside `2:32 PM` is the answer itself. The time is the one the dashboards' own
        /// clock is drawn with at design time.</summary>
        public static readonly string[] ClockLabels = { "14:32", "2:32 PM" };
    }
}
