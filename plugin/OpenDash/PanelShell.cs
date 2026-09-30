// PanelShell.cs: the settings panel's frame as numbers -- the sidebar, the main column, the sheet and the
// three widths the panel reads at -- and the route that says which page is showing.
//
// #503 replaces four tabs with a sidebar of pages, one per thing on the rig. The frame is the one part every
// page shares and no page may change, so its geometry is here, where PanelShellTests holds it to the
// redesign's artboards, rather than in SettingsControl.cs, which no test can compile. The artboards live in
// the redesign canvas and are not yet under design/; the literals the tests pin move to a join with
// design/canvas/plugin/*.dc.html once the author copies them there.
//
// Pure: no WPF and no SimHub types. Compiled into OpenDash.Tests by the Panel*.cs wildcard.
using System;

namespace OpenDashPlugin
{
    /// <summary>The panel's pages, in the order the sidebar lists them. Updates is pinned to the sidebar's
    /// foot rather than listed with the rest, and is last here for that reason.</summary>
    public enum PanelPage
    {
        Home,
        Rig,
        Screens,
        Leds,
        Matrix,
        Shortcuts,
        Settings,
        Updates,
    }

    /// <summary>What a message says about what happened: nothing to act on, something to look at, or a
    /// failure.</summary>
    public enum PanelTone
    {
        Info,
        Caution,
        Danger,
    }

    /// <summary>The three widths the panel reads at.</summary>
    public enum PanelLayout
    {
        /// <summary>The full sidebar with its labels, and two-column blocks where the content allows.</summary>
        Full,

        /// <summary>The sidebar as a rail of icons, and every block stacked.</summary>
        Rail,

        /// <summary>The rail, a narrower gutter, and card grids of at most two columns.</summary>
        Compact,
    }

    /// <summary>
    /// A line the panel says after something happened, at the top of the page, until the next page is drawn.
    /// </summary>
    public sealed class PanelMessage
    {
        public PanelMessage(string text, PanelTone tone)
        {
            Text = text ?? string.Empty;
            Tone = tone;
        }

        public string Text { get; private set; }

        public PanelTone Tone { get; private set; }

        public static PanelMessage Info(string text) { return new PanelMessage(text, PanelTone.Info); }

        public static PanelMessage Caution(string text) { return new PanelMessage(text, PanelTone.Caution); }

        public static PanelMessage Danger(string text) { return new PanelMessage(text, PanelTone.Danger); }

        /// <summary>A message that is the ordinary line when it worked and a caution when it did not, which
        /// is the shape almost every press answers in.</summary>
        public static PanelMessage Of(bool ok, string text) { return new PanelMessage(text, ok ? PanelTone.Info : PanelTone.Caution); }

        /// <summary>The ink a tone is drawn in: text.secondary for a line, caution and danger for the other two.</summary>
        public static string Ink(PanelTone tone)
        {
            switch (tone)
            {
                case PanelTone.Caution: return Theme.Caution;
                case PanelTone.Danger: return Theme.Danger;
                default: return Theme.TextSecondary;
            }
        }
    }

    /// <summary>
    /// Where the panel is: a page, and optionally a row on it to scroll to.
    /// </summary>
    /// <remarks>
    /// Held for the session and never persisted, as the tab was: somebody who came to change a zone lands
    /// where they left off within one sitting, and somebody coming back next week lands on Home. Which card
    /// on a page is selected is held beside it by the shell, per page, for the same session.
    /// </remarks>
    public sealed class PanelRoute : IEquatable<PanelRoute>
    {
        public PanelRoute(PanelPage page, string anchor = null)
        {
            Page = page;
            Anchor = string.IsNullOrEmpty(anchor) ? null : anchor;
        }

        public PanelPage Page { get; private set; }

        /// <summary>The id of the row to bring into view once the page is drawn, or null for the top.</summary>
        public string Anchor { get; private set; }

        public static readonly PanelRoute Home = new PanelRoute(PanelPage.Home);

        public static PanelRoute To(PanelPage page, string anchor = null) { return new PanelRoute(page, anchor); }

        public bool Equals(PanelRoute other)
        {
            return !ReferenceEquals(other, null) && Page == other.Page && string.Equals(Anchor, other.Anchor, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as PanelRoute); }

        public override int GetHashCode() { return ((int)Page * 397) ^ (Anchor == null ? 0 : StringComparer.Ordinal.GetHashCode(Anchor)); }

        public override string ToString() { return Anchor == null ? Page.ToString() : Page + "#" + Anchor; }
    }

    /// <summary>Every number the frame is drawn with, read off Sidebar.dc.html and the page artboards.</summary>
    public static class PanelShell
    {
        // --- The three widths ------------------------------------------------------------------------

        /// <summary>At this control width and above the sidebar is drawn in full.</summary>
        public const double FullFrom = 1000;

        /// <summary>At this width and above, below <see cref="FullFrom"/>, the sidebar is a rail and the main
        /// column keeps a 32 px gutter; below it the gutter is 20 and a card grid holds two columns.</summary>
        public const double RailFrom = 760;

        public static PanelLayout Layout(double controlWidth)
        {
            if (controlWidth >= FullFrom) return PanelLayout.Full;
            return controlWidth >= RailFrom ? PanelLayout.Rail : PanelLayout.Compact;
        }

        /// <summary>Whether the layout draws the sidebar as a rail of icons.</summary>
        public static bool IsNarrow(PanelLayout layout) { return layout != PanelLayout.Full; }

        // --- The sidebar -------------------------------------------------------------------------------

        public const double SidebarWidth = 216;
        public const double RailWidth = 56;
        public const double SidebarPaddingTop = 22;
        public const double SidebarPaddingX = 12;
        public const double RailPaddingX = 8;
        public const double SidebarPaddingBottom = 14;

        /// <summary>The mark, the wordmark and the version, on a row of the wordmark's own height.</summary>
        public const double MarkRowHeight = 21;
        public const double MarkRowGap = 20;
        public const double MarkRowPaddingX = 8;
        public const double MarkSize = 18;
        public const double WordmarkSize = 21;
        public const double VersionSize = 14;

        public const double SearchHeight = 34;
        public const double SearchGap = 14;
        public const double SearchPaddingX = 10;
        public const double SearchIconSize = 14;
        public const double SearchTextSize = 13;

        /// <summary>
        /// The live card, which is drawn at one height whatever it says so that the pages below it never
        /// move when a session starts: an eyebrow row of 14, a car line of 18 and a track line of 16, 5 apart,
        /// inside 12 above, 13 below and a one pixel border.
        /// </summary>
        public const double LiveCardHeight = 85;
        public const double LiveCardGap = 18;
        public const double LiveCardPaddingX = 12;
        public const double LiveCardPaddingTop = 12;
        public const double LiveCardPaddingBottom = 13;
        public const double LiveLineGap = 5;
        public const double LiveEyebrowHeight = 14;
        public const double LiveCarHeight = 18;
        public const double LiveTrackHeight = 16;
        public const double LiveDotSize = 7;

        /// <summary>The ring round a live dot: 3 px of the dot's own green at 18 %.</summary>
        public const double LiveRingWidth = 3;
        public const double LiveRingOpacity = 0.18;

        /// <summary>The live card on the rail: its dot alone, with the card's words as its tooltip.</summary>
        public const double RailLiveHeight = 22;

        public const double NavItemHeight = 40;
        public const double NavItemGap = 2;
        public const double NavItemPaddingX = 10;
        public const double NavIconGap = 12;
        public const double NavTextSize = 15;
        public const double NavCountSize = 15;
        public const double NavWarnSize = 7;

        /// <summary>What sits between an item's label, its amber dot and its count: the artboard's flex gap.</summary>
        public const double NavTrailGap = 12;

        /// <summary>The accent bar down the left edge of the page that is showing.</summary>
        public const double NavActiveBar = 2;

        /// <summary>The rule after Rig and after Matrix: one pixel with eight above and below it, an item of
        /// its own in the column, so the column's 2 px gap falls on both sides of it as well.</summary>
        public const double NavDividerMarginY = 8;
        public const double NavDividerMarginX = 10;
        public const double NavDividerHeight = 1 + 2 * NavDividerMarginY;

        /// <summary>The foot: night mode over a rule, and Updates pinned under it.</summary>
        public const double FootPaddingY = 14;
        public const double FootPaddingX = 8;
        public const double UpdatesGap = 4;
        public const double BadgeHeight = 20;
        public const double BadgePaddingX = 6;
        public const double BadgeTextSize = 13;

        public const double SwitchWidth = 40;
        public const double SwitchHeight = 22;
        public const double SwitchKnob = 16;
        public const double SwitchInset = 3;

        public static double SidebarWidthFor(PanelLayout layout) { return layout == PanelLayout.Full ? SidebarWidth : RailWidth; }

        /// <summary>
        /// The room inside the rail: its 56 less the one pixel rule down its right edge and 8 each side, which
        /// is 39 and not 40. What the rail holds is drawn to this, so nothing in it is clipped by a pixel.
        /// </summary>
        public const double RailInnerWidth = RailWidth - PanelMetrics.BorderWeight - 2 * RailPaddingX;

        /// <summary>
        /// Where the centre of a sidebar item sits across, from the sidebar's left edge: half the room inside
        /// the rule, 107.5 on the full sidebar and 27.5 on the rail. A capture script clicks here and at
        /// <see cref="ItemCentre"/>, so the x follows the layout the panel is in as the y does.
        /// </summary>
        public static double ItemCentreX(PanelLayout layout = PanelLayout.Full)
        {
            return (SidebarWidthFor(layout) - PanelMetrics.BorderWeight) / 2;
        }

        /// <summary>
        /// Where the centre of a sidebar item sits, from the sidebar's top: the index is the item's place
        /// in <see cref="PanelNav.Pages"/>, Home being 0.
        /// </summary>
        /// <remarks>
        /// A capture script mirrors this to click a page without reading the tree, which is why it is a
        /// function of the geometry rather than a measurement. The rail puts its items at the same heights
        /// less the live card's difference, since the card shrinks to its dot there: 63 px higher than on the
        /// full sidebar, at <see cref="ItemCentreX"/>'s 27.5 across rather than 107.5. A mirror takes the
        /// layout from <see cref="Layout"/> of the plugin control's width, not SimHub's window's, clicks
        /// (ItemCentreX(layout), ItemCentre(i, layout)) and <see cref="UpdatesCentre"/> for Updates, and carries
        /// PanelNav.Pages' order, PanelNav.GapAfter and PanelMetrics.BorderWeight as well as the constants
        /// here, since all three feed these numbers.
        /// </remarks>
        public static double ItemCentre(int index, PanelLayout layout = PanelLayout.Full)
        {
            if (index < 0 || index >= PanelNav.Pages.Length) throw new ArgumentOutOfRangeException("index", index, "not an item the sidebar lists");
            var y = SidebarPaddingTop + MarkRowHeight + MarkRowGap + SearchHeight + SearchGap
                + (layout == PanelLayout.Full ? LiveCardHeight : RailLiveHeight) + LiveCardGap;
            for (var i = 0; i < index; i++)
            {
                y += NavItemHeight + NavItemGap;
                if (PanelNav.GapAfter(PanelNav.Pages[i])) y += NavDividerHeight + NavItemGap;
            }
            return y + NavItemHeight / 2;
        }

        /// <summary>Where the centre of the Updates item sits in a sidebar of that height: it is pinned to
        /// the foot, so it is measured up from the bottom.</summary>
        public static double UpdatesCentre(double sidebarHeight)
        {
            return sidebarHeight - SidebarPaddingBottom - NavItemHeight / 2;
        }

        // --- The main column ---------------------------------------------------------------------------

        /// <summary>The main column's gutter at each width: 44 beside the full sidebar, 32 beside the rail,
        /// 20 at the narrowest.</summary>
        public static double MainPaddingX(PanelLayout layout)
        {
            switch (layout)
            {
                case PanelLayout.Full: return 44;
                case PanelLayout.Rail: return 32;
                default: return 20;
            }
        }

        public const double MainPaddingTop = 36;
        public const double MainPaddingBottom = 32;

        /// <summary>The main column's padding under a page: 32 on every artboard but Settings.dc.html's,
        /// whose &lt;main&gt; is "36px 44px 40px".</summary>
        public const double MainPaddingBottomSettings = 40;

        public static double MainPaddingBottomFor(PanelPage page)
        {
            return page == PanelPage.Settings ? MainPaddingBottomSettings : MainPaddingBottom;
        }

        /// <summary>What a page's content grows to and not past. The Rig page may opt out, since its canvas
        /// is a drawing of the rig and is better for the room.</summary>
        public const double ContentMax = 1112;

        /// <summary>Whether a page may lay its blocks side by side: only beside the full sidebar and only
        /// where the content has at least this much room.</summary>
        public const double TwoColumnFrom = 760;

        /// <summary>The gap between a page's sections on Home, whose &lt;main&gt; Main.dc.html lays out at 28, and
        /// on Settings, whose sections carry the same 28 as their own padding (.sec).</summary>
        public const double SectionGap = 28;

        /// <summary>
        /// The gap between a page's sections, which each artboard's &lt;main&gt; draws at its own: 28 on Home and
        /// Settings, 22 on Screens, LEDs, Matrix and Shortcuts, 26 on Updates, 18 on Rig. PageLayout reads it
        /// for the page that is showing.
        /// </summary>
        public static double SectionGapFor(PanelPage page)
        {
            switch (page)
            {
                case PanelPage.Screens:
                case PanelPage.Leds:
                case PanelPage.Matrix:
                case PanelPage.Shortcuts:
                    return 22;
                case PanelPage.Updates:
                    return 26;
                case PanelPage.Rig:
                    return 18;
                default:
                    return SectionGap;
            }
        }

        /// <summary>
        /// The room a page has to lay out in, for a control of that width, less <paramref name="scrollBar"/>:
        /// the main column's vertical scroll bar, which takes its width from the column whenever the page is
        /// taller than the window, and which the shell always leaves room for rather than rebuild the page
        /// each time the bar comes and goes.
        /// </summary>
        public static double ContentWidth(double controlWidth, bool wide = false, double scrollBar = 0)
        {
            var layout = Layout(controlWidth);
            var room = controlWidth - SidebarWidthFor(layout) - 2 * MainPaddingX(layout) - Math.Max(0, scrollBar);
            if (room < 0) room = 0;
            return wide ? room : Math.Min(room, ContentMax);
        }

        /// <summary>How long a resize waits for the window to stop moving before the page is rebuilt.</summary>
        public const int ResizeSettleMs = 150;

        /// <summary>How long a lighting change waits for a burst of wheel presses to stop before a page that
        /// draws lighting is rebuilt.</summary>
        public const int LightingSettleMs = 120;

        public static bool TwoColumns(PanelLayout layout, double contentWidth)
        {
            return layout == PanelLayout.Full && contentWidth >= TwoColumnFrom;
        }

        /// <summary>
        /// How many columns a grid of cards takes in the room it has: as many cards of at least that width as
        /// fit with the gap between them, at least one, never more than the cap, and never more than two at
        /// the narrowest layout.
        /// </summary>
        public static int Columns(double available, double minCard, double gap, int max, PanelLayout layout = PanelLayout.Full)
        {
            if (max < 1) max = 1;
            if (layout == PanelLayout.Compact && max > 2) max = 2;
            if (minCard <= 0 || available <= 0) return 1;
            var fit = (int)Math.Floor((available + gap) / (minCard + gap));
            if (fit < 1) fit = 1;
            return fit > max ? max : fit;
        }

        // --- The sheet -----------------------------------------------------------------------------------

        /// <summary>A sheet is this wide beside the cards, and takes the whole main column below
        /// <see cref="SheetFullFrom"/>.</summary>
        public const double SheetWidthMax = 560;
        public const double SheetFullFrom = 900;
        public const double SheetPaddingX = 28;
        public const double SheetHeaderPaddingTop = 24;
        public const double SheetHeaderPaddingBottom = 18;
        public const double SheetTitleSize = 22;
        public const double SheetFooterPaddingTop = 18;
        public const double SheetFooterPaddingBottom = 24;

        /// <summary>The body's padding is "0 28px" on both Add sheets: nothing under the last step, whose own
        /// 20 below is what separates it from the footer.</summary>
        public const double SheetBodyPaddingBottom = 0;

        /// <summary>The dim laid over the main column while a sheet is open.</summary>
        public const double SheetDimOpacity = 0.6;

        /// <summary>How wide a sheet is in a control of that width.</summary>
        public static double SheetWidth(double controlWidth)
        {
            var column = controlWidth - SidebarWidthFor(Layout(controlWidth));
            if (column < 0) column = 0;
            if (controlWidth < SheetFullFrom) return column;
            return Math.Min(SheetWidthMax, column);
        }

        /// <summary>
        /// How far the sheet's right edge stands in from the control's: the room right of the main column,
        /// which stops at <see cref="ContentMax"/> and its gutters unless the page is wide. The sheet opens
        /// beside the cards, so at 3840 px it lies against the column's edge near 1416 and not 1860 px away
        /// at the control's.
        /// </summary>
        public static double SheetRightGap(double controlWidth, bool wide = false)
        {
            if (wide) return 0;
            var layout = Layout(controlWidth);
            var gap = controlWidth - SidebarWidthFor(layout) - (ContentMax + 2 * MainPaddingX(layout));
            return gap > 0 ? gap : 0;
        }

        // --- What the pages share -----------------------------------------------------------------------

        /// <summary>What a page says in place of itself when drawing it threw.</summary>
        public const string PageFailed = "This page could not be drawn. See SimHub's log.";

        /// <summary>A control that is drawn and not yet built: faded to this, and not answering.</summary>
        public const double SoonOpacity = 0.45;

        public const double PageTitleSize = 30;
        public const double HeadingSize = 17;
        public const double HeadingLargeSize = 19;
        public const double SubHeadingSize = 22;
        public const double EyebrowSize = 11;
        public const double RowTitleSize = 15;
        public const double RowPaddingY = 12;
        public const double RowGap = 24;
        public const double SubRowIndent = 16;
        public const double TagHeight = 18;
        public const double TagPaddingX = 6;
        public const double TagTextSize = 10;
        public const double CrumbHeight = 22;
        public const double CrumbPaddingX = 7;
        public const double CrumbTextSize = 12;
        public const double MessageTextSize = 13;

        /// <summary>The pages' .inp: 30 high, 10 in, 13 px.</summary>
        public const double InputHeight = 30;
        public const double InputPaddingX = 10;
        public const double InputTextSize = 13;

        /// <summary>The pages' .num-in: 64 by 30, 8 in, the value at 15.</summary>
        public const double NumberInputWidth = 64;
        public const double NumberInputPaddingX = 8;
        public const double NumberInputTextSize = 15;
    }
}
