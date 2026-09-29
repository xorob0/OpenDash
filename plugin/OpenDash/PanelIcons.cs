// PanelIcons.cs: the settings panel's icons, in the coordinates design/canvas/PluginComponents.dc.html
// draws them.
//
// Kept apart from Widgets.cs for the reason Mark.cs is: the panel is WPF and the net8.0 test project cannot
// compile a line of it, so the paths live where PanelIconsTests can hold them against the sheet itself
// rather than against a second transcription of it. That is the join MarkTests makes with media/logo.svg,
// and it is the only one that catches a path drifting. Pure: no WPF and no SimHub types.
namespace OpenDashPlugin
{
    /// <summary>
    /// One stroke set, drawn on a sixteen unit box and scaled to whatever the caller wants.
    /// </summary>
    /// <remarks>
    /// The sheet describes the whole set in one line: "16 in controls, 20 alone, 1.5 stroke, square caps,
    /// mitre joins." The two sizes and the weight are below; the caps and the joins are WPF enumerations
    /// rather than numbers, so <c>Ui.Icon</c> keeps them and there is nothing here for a test to hold.
    /// </remarks>
    public static class PanelIcons
    {
        /// <summary>The side of the square every path is drawn in, which is the viewBox the sheet gives
        /// each icon.</summary>
        public const double Box = 16;

        /// <summary>An icon inside a control, which is the control.icon token.</summary>
        public const double SizeInControl = Theme.IconSize;

        /// <summary>An icon standing on its own, which the sheet draws half as large again as the box it
        /// is described on. Nothing in the panel draws one yet.</summary>
        public const double SizeAlone = 20;

        /// <summary>One weight for the whole set, so that a row of icons reads as one hand.</summary>
        public const double StrokeWeight = 1.5;

        // The eight the sheet's icon row carries, in the order it lays them out. Each string is the sheet's
        // own `d` rather than a copy of it, which is what PanelIconsTests compares character for character.

        /// <summary>A tray with an arrow coming down into it.</summary>
        public const string Install = "M8 2v9M4 7l4 4 4-4M2.5 13.5h11";

        /// <summary>Two arcs chasing each other, each with a tick on its open end.</summary>
        public const string Refresh = "M13 8a5 5 0 0 1-8.7 3.4M3 8a5 5 0 0 1 8.7-3.4M11.5 2v3h-3M4.5 14v-3h3";

        /// <summary>The chevron a drop-down carries, pointing down when it is shut.</summary>
        public const string Chevron = "M4 6l4 4 4-4";

        /// <summary>The tick an install that worked shows.</summary>
        public const string Check = "M3 8.5l3.5 3.5L13 5";

        /// <summary>The warning triangle, bang and all.</summary>
        public const string Alert = "M8 2l6.5 11.5h-13zM8 6.5v3.5M8 11.5v.5";

        /// <summary>A box with an arrow leaving it, for a link that opens outside SimHub.</summary>
        public const string External = "M6.5 3.5H3.5v9h9V9.5M9 3h4v4M13 3l-6 6";

        /// <summary>A face is a display: a screen on a short stand.</summary>
        public const string Display = "M2 3h12v8H2zM6 13.5h4";

        /// <summary>A pit wall is a grid of four squares, because it is four pages of zones.</summary>
        public const string Grid = "M2.5 2.5h4v4h-4zM9.5 2.5h4v4h-4zM2.5 9.5h4v4h-4zM9.5 9.5h4v4h-4z";

        // Two the panel draws that the sheet's icon row does not carry. They were already in the tree and
        // are already on the cards, so they stay; but no artboard declares them, which is why the test
        // below pins them against these literals rather than against PluginComponents.dc.html, and why
        // docs/research/design-audit.md is where the author is asked whether the sheet should gain them.

        /// <summary>A companion is a phone: a tall rectangle with a home line across its foot.</summary>
        public const string Phone = "M4.5 1.5h7v13h-7zM6.5 12.5h3";

        /// <summary>The plus the add card carries.</summary>
        public const string Plus = "M8 3v10M3 8h10";

        // The rebuilt panel's own set (#503), drawn on a twenty unit box as the redesign's artboards draw
        // them: the sidebar's eight, and the few glyphs the pages put beside a row. None of them is on
        // PluginComponents.dc.html, whose icon row a test holds to exactly eight names, so PanelIconsTests
        // pins these against their literals the way it pins the phone and the plus; they move to a join
        // with design/canvas/plugin/*.dc.html once the author copies the artboards there.

        /// <summary>The side of the square the sidebar's paths are drawn in.</summary>
        public const double NavBox = 20;

        /// <summary>A sidebar icon on screen, which is the artboard's 18.</summary>
        public const double NavSize = 18;

        /// <summary>Home: a house.</summary>
        public const string Home = "M3 9l7-5.5L17 9v8H3z M8 17v-5h4v5";

        /// <summary>Rig: a screen, a box beside it and a strip under both, which is a rig seen from the seat.</summary>
        public const string Rig = "M2.5 3.5h9v6h-9z M13.5 3.5h4v4h-4z M2.5 12.5h15v3h-15z";

        /// <summary>Screens: one display on a short stand.</summary>
        public const string Screens = "M2.5 4.5h15v9h-15z M7 17h6";

        /// <summary>LEDs: three lamps in a row.</summary>
        public const string Leds = "M2.5 8.5h3v3h-3z M8.5 8.5h3v3h-3z M14.5 8.5h3v3h-3z";

        /// <summary>Matrix: a three by three grid, which is the 8x8 at the size of an icon.</summary>
        public const string Matrix = "M3 3h4v4H3z M8 3h4v4H8z M13 3h4v4h-4z M3 8h4v4H3z M8 8h4v4H8z M13 8h4v4h-4z M3 13h4v4H3z M8 13h4v4H8z M13 13h4v4h-4z";

        /// <summary>Shortcuts: a button, a ring round a smaller one.</summary>
        public const string Shortcuts = "M10 3a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M10 7.5a2.5 2.5 0 1 0 0 5a2.5 2.5 0 1 0 0-5z";

        /// <summary>Settings: two sliders.</summary>
        public const string Settings = "M3 6h14 M3 14h14 M7 4v4 M13 12v4";

        /// <summary>Updates: an arrow down into a tray.</summary>
        public const string Updates = "M10 3v9M6 8.5l4 4 4-4M3.5 14v3h13v-3";

        /// <summary>The search field's lens.</summary>
        public const string Search = "M8.5 3a5.5 5.5 0 1 0 0 11a5.5 5.5 0 1 0 0-11z M12.5 12.5l4.5 4.5";

        /// <summary>The cross that closes a sheet.</summary>
        public const string Close = "M5 5l10 10M15 5L5 15";

        /// <summary>The ringed bang a notice and a fix box carry.</summary>
        public const string Warning = "M10 3a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M10 6v5 M10 13.5v.5";

        /// <summary>An arc with an arrowhead: a restart, or a check done again.</summary>
        public const string Restart = "M15.5 6.5A6.5 6.5 0 1 0 16.5 11 M16 3v4h-4";

        /// <summary>The six-dot grip a row that can be dragged carries, as two columns of short marks.</summary>
        public const string DragHandle = "M7.5 4.5v1 M12.5 4.5v1 M7.5 9.5v1 M12.5 9.5v1 M7.5 14.5v1 M12.5 14.5v1";

        /// <summary>A chevron pointing on, which separates two crumbs.</summary>
        public const string ChevronRight = "M7.5 5l5 5-5 5";

        /// <summary>A chevron pointing down, which a drop-down and a disclosure carry.</summary>
        public const string ChevronDown = "M5 7.5l5 5 5-5";

        /// <summary>The plus a dashed add card carries at the twenty unit size.</summary>
        public const string Add = "M10 4v12M4 10h12";
    }
}
