// PanelFacePlan.cs: the picture of a face the Screens page configures a screen on, as numbers.
//
// A screen is configured on a picture of itself, because "zone C" means nothing until you see where
// zone C is. The picture is the face's own rectangles scaled to the width the page gives it, which is
// what stops one face's proportions being drawn for every face; before this class the four rows were
// four constants, so a 1920 x 480 face whose rows are 48, 56, 314 and 60 was drawn 19, 24, 150 and 26.
//
// Screens.dc.html draws the picture as a strip of twenty rev segments, the info bar, the three zones and
// band D, each of them a press that opens what it shows in the aside beside it, 5 apart inside an 8 px
// inset. Apart from SettingsControl.Screens.Face.cs for the reason PanelMetrics.cs is apart from
// Widgets.cs: the panel is WPF and the net8.0 test project cannot compile a line of it, so the geometry
// lives where PanelFacePlanTests can hold it against the canvas. Pure: no WPF types.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>The rows and the cells of one face's picture, at the width the page draws it.</summary>
    public sealed class PanelFacePlan
    {
        /// <summary>The rows' width For(face) takes when nothing says otherwise: the old pane's body, kept so
        /// every face's own proportions are held at one width. Not the width the page draws: that is
        /// <see cref="PictureWidthFor"/> of the page, fitted by <see cref="FitWidth"/>.</summary>
        public const double PictureWidth = 844;

        /// <summary>The gap between two rows of the picture, and between two cells: the artboard's 5.</summary>
        public const double Seam = 5;

        /// <summary>The inset ground around the rows, and the rule around that: the artboard's 8 and 1.</summary>
        public const double Inset = 8;

        public const double Frame = 1;

        // The least a row can be and still hold what it draws. A row of the picture is a press carrying
        // words rather than a band of pixels, so where a face's own rectangle scales to less than its words
        // need, the row keeps the words and stops being to scale.

        /// <summary>The rev strip: the artboard's 16, a row of segments 3 in from its edges.</summary>
        public const double RevBarLeast = 16;

        /// <summary>The info bar: one line of 11 px words, the artboard's 26 less the border it lends.</summary>
        public const double BarLeast = Theme.ControlHeightSm;

        /// <summary>Band D: its letter, first page, count and button along one line, the artboard's 40 less
        /// what the border and the padding share.</summary>
        public const double BandLeast = 36;

        /// <summary>The line height of the panel's fonts, as a multiple of their size: Barlow and openDash
        /// Display set USE_TYPO_METRICS, and their typo metrics come to 1.2.</summary>
        public const double LineHeight = 1.2;

        /// <summary>
        /// The least a zone cell can be: its border and padding above and below, the letter and count, the
        /// first page and the button line under it, six apart.
        /// </summary>
        /// <remarks>
        /// 1 + 9 + 21.6 + 6 + 18 + 6 + 14.4 + 9 + 1, which is 86: each line at <see cref="LineHeight"/> of its
        /// size, not at its size, or the page line is squeezed and its descenders cut. It binds wherever a
        /// face's own body scales to less, which for the reference face is any picture narrower than about
        /// 530 px of rows: a narrow page, or the stacked picture of a phone-width one.
        /// </remarks>
        public const double CellLeast = 86;

        /// <summary>A cell is padded 9 above and below and 10 at the sides (the artboard's .zone).</summary>
        public const double CellPaddingX = 10;

        public const double CellPaddingY = 9;

        /// <summary>Between the letter row, the first page and the button line.</summary>
        public const double CellGap = 6;

        /// <summary>A zone's letter, in the display family (.num at 18); band D's at 16.</summary>
        public const double LetterSize = 18;

        public const double BandLetterSize = 16;

        /// <summary>The count beside a letter ("4 of 21"), the first page under it, and the button line.</summary>
        public const double CountSize = 13;

        public const double PageSize = 15;

        public const double BandPageSize = 14;

        public const double ButtonLineSize = 12;

        /// <summary>The info bar's words: the artboard's 11, in text.secondary, the middle in text.label.</summary>
        public const double BarTextSize = 11;

        /// <summary>A selected cell or bar is outlined 2 in the accent, the others 1 in the rule.</summary>
        public const double SelectedEdge = 2;

        /// <summary>The aside beside the picture (the Info bar or the selected zone), and the gap between
        /// them, when the page has two columns; under the picture otherwise. The aside is at least this wide,
        /// and takes whatever the column leaves past the picture's.</summary>
        public const double AsideWidth = 316;

        public const double AsideGap = 24;

        /// <summary>The room the picture has on a page <paramref name="content"/> wide: beside the aside's
        /// least in two columns, the whole width stacked. The aside stands beside the picture whenever the page
        /// has two columns (TwoColumns, from 760 of content), as the pit wall's list and the round screen's
        /// rows do, and under it otherwise.</summary>
        public static double PictureWidthFor(double content, bool twoColumns)
        {
            return Math.Max(0, twoColumns ? content - AsideWidth - AsideGap : content);
        }

        /// <summary>The picture's column the artboard draws, 896 less the aside and the gap: the least the
        /// column beside the aside is laid out at where the page has the room, so the rows under a picture
        /// that its height holds narrower (the portrait face) keep the artboard's width.</summary>
        public const double ColumnLeast = 556;

        /// <summary>
        /// The column the picture and the rows under it take beside the aside, on a page
        /// <paramref name="content"/> wide: as wide as the fitted picture, or <see cref="ColumnLeast"/> where
        /// the picture is narrower, and never wider than the room beside the aside's least.
        /// </summary>
        /// <remarks>
        /// The aside takes the rest of the content, as the pit wall's zone list does beside its capped
        /// picture: a column that grew with the window while the picture stopped at its height left the aside
        /// docked at the far edge, 1600 px and more from the zone it lists on a 4K window.
        /// </remarks>
        public static double ColumnFor(Contract.FaceSize face, double content)
        {
            var room = PictureWidthFor(content, true);
            return Math.Min(room, Math.Max(ColumnLeast, FitWidth(face, room)));
        }

        /// <summary>
        /// The widest the picture of <paramref name="face"/> is ever drawn: where <see cref="MaxHeight"/> stops
        /// it, whatever the column. <see cref="FitWidth"/> of any column is that column or this, whichever is
        /// less.
        /// </summary>
        /// <remarks>
        /// Found by halving rather than by FitWidth of an endless column, whose loop walks down one pixel at a
        /// time from the width it is given and would never end.
        /// </remarks>
        public static double FitMost(Contract.FaceSize face)
        {
            var frame = 2 * (Inset + Frame);
            var cap = Math.Max(MaxHeight, For(face, 1).Height + frame);
            Func<double, bool> fits = width => For(face, RowsWidth(width)).Height + frame <= cap;
            var low = Math.Floor(frame + 1);
            if (!fits(low)) return low;
            double high = 1 << 16;
            while (high - low > 1)
            {
                var middle = Math.Floor((low + high) / 2);
                if (fits(middle)) low = middle;
                else high = middle;
            }
            return low;
        }

        /// <summary>
        /// The most of the content width the face's editor reads (ContentWidthUpTo): past it nothing the
        /// editor draws changes, so a wider window does not rebuild the page and reload its live preview.
        /// </summary>
        /// <remarks>
        /// Beside the aside, the column stops at the widest picture or <see cref="ColumnLeast"/>, and the aside
        /// takes the rest without a rebuild. Stacked, the picture stops at its widest and the Quick glance's
        /// controls stop wrapping at PanelScreens.ControlsColumnMost; the rows stretch without a rebuild.
        /// </remarks>
        public static double ContentMost(Contract.FaceSize face, bool twoColumns)
        {
            return twoColumns
                ? Math.Max(ColumnLeast, FitMost(face)) + AsideGap + AsideWidth
                : Math.Max(FitMost(face), PanelScreens.ControlsColumnMost);
        }

        /// <summary>The tallest the picture is drawn, frame included: a portrait face as wide as the column is
        /// taller than SimHub's window, and would push the rows and the aside out of sight.</summary>
        public const double MaxHeight = 420;

        /// <summary>
        /// The width the picture is drawn at in a column <paramref name="outer"/> wide: the column, or less
        /// where the face at that width would stand taller than <see cref="MaxHeight"/>.
        /// </summary>
        /// <remarks>
        /// A portrait face whose rows are at their least is still taller than the cap (three cells of at
        /// least <see cref="CellLeast"/> each), so the width stops shrinking where the height stops falling.
        /// </remarks>
        public static double FitWidth(Contract.FaceSize face, double outer)
        {
            var width = Math.Floor(Math.Max(0, outer));
            var frame = 2 * (Inset + Frame);
            var cap = Math.Max(MaxHeight, For(face, 1).Height + frame);
            while (width > frame + 1 && For(face, RowsWidth(width)).Height + frame > cap) width--;
            return width;
        }

        /// <summary>
        /// The widest band D's button line is drawn in a picture whose rows are <paramref name="rowsWidth"/>
        /// across: a third of the band inside its rule and padding, cut short past it.
        /// </summary>
        /// <remarks>
        /// The page is the band's content and the button line is about the band, so the line is what gives
        /// way. Uncapped, "FANATEC Podium Wheel Base DD1 · 12" kept its 195 px on the portrait face's 315 px
        /// band and cut Relative to "Relati…", and a longer device name left no room for the page at all.
        /// </remarks>
        public static double BandButtonMax(double rowsWidth)
        {
            return Math.Max(0, Math.Floor((rowsWidth - 2 * CellPaddingX - 2 * PanelMetrics.BorderWeight) / 3));
        }

        /// <summary>The widest a binding chip is drawn in the zone aside, cut short past it: what leaves
        /// "Previous page" and its NEW tag their 131 px of the aside's 282.</summary>
        public const double AsideChipMax = 136;

        /// <summary>The rev strip's padding round its segments: the artboard's 3 above and below, 5 at the sides.</summary>
        public const double RevPaddingX = 5;

        public const double RevPaddingY = 3;

        /// <summary>The info bar's three cells, 3 : 4 : 3 across (the artboard's flex 3, 4 and 3), padded 8 at
        /// its sides.</summary>
        public const int BarEndShare = 3;

        public const int BarMiddleShare = 4;

        public const double BarPaddingX = 8;

        /// <summary>Between band D's letter, its page, its count and its button line: the artboard's gap 12.</summary>
        public const double BandGap = 12;

        /// <summary>The rev strip's segments: twenty, 3 apart, inside 3 by 5 of padding.</summary>
        public const int RevSegments = 20;

        public const double RevGap = 3;

        /// <summary>Nine green, five amber, two red and four unlit, as the artboard draws a mid-range shift.</summary>
        public static string[] RevColours(bool on)
        {
            var colours = new string[RevSegments];
            for (var i = 0; i < RevSegments; i++)
            {
                colours[i] = !on ? Theme.SurfaceRaised
                    : i < 9 ? Theme.ShiftStage1
                    : i < 14 ? Theme.ShiftStage2
                    : i < 16 ? Theme.ShiftStage3
                    : Theme.SurfaceRaised;
            }
            return colours;
        }

        private PanelFacePlan(Contract.FaceSize face, double width)
        {
            Width = width;
            var scale = width / face.Width;
            Stacked = face.Body == Contract.FaceBody.Column;
            HasBar = face.HasBar;
            RevBar = Row(face.RevBarHeight, scale, RevBarLeast);
            Bar = face.HasBar ? Row(face.BarHeight, scale, BarLeast) : 0;
            Body = Stacked
                ? StackedBody(Row(face.BodyHeight, scale, face.Parts.Length * CellLeast + (face.Parts.Length - 1) * Seam), face.Parts)
                : Row(face.BodyHeight, scale, CellLeast);
            Band = Row(face.BandHeight, scale, BandLeast);
            Letters = face.BodyOrder;
            Cells = Split(Stacked ? Body : width, face.Parts);
        }

        /// <summary>
        /// A stacked body of at least <paramref name="body"/>, grown until every one of its cells is at
        /// <see cref="CellLeast"/> or more.
        /// </summary>
        /// <remarks>
        /// The body is split in proportion to the face's own parts, so a floor of three leasts laid end to
        /// end is not a floor for each cell: the 600 x 686 face's parts are 234, 160 and 150, and a body of
        /// 268 or 306 split that way drew zone C 71 or 82 px tall, cutting its page line's descenders. The
        /// cells are held as Split draws them, rounding included, at whatever width the body scales to.
        /// </remarks>
        private static double StackedBody(double body, int[] parts)
        {
            while (Split(body, parts).Min() < CellLeast) body++;
            return body;
        }

        /// <summary>The picture of one face at <see cref="PictureWidth"/>.</summary>
        public static PanelFacePlan For(Contract.FaceSize face)
        {
            return For(face, PictureWidth);
        }

        /// <summary>The picture of one face with its rows <paramref name="width"/> across: the room the page
        /// has for the picture, less the inset and the frame on both sides (see <see cref="RowsWidth"/>).</summary>
        public static PanelFacePlan For(Contract.FaceSize face, double width)
        {
            return new PanelFacePlan(face, Math.Max(1, Math.Floor(width)));
        }

        /// <summary>The width the rows take inside a picture <paramref name="outer"/> wide.</summary>
        public static double RowsWidth(double outer)
        {
            return outer - 2 * (Inset + Frame);
        }

        /// <summary>The width the rows are drawn across.</summary>
        public double Width { get; private set; }

        /// <summary>Zone A over zone B over zone C, rather than side by side.</summary>
        public bool Stacked { get; private set; }

        /// <summary>False on the nano, whose picture has three rows and not four.</summary>
        public bool HasBar { get; private set; }

        public double RevBar { get; private set; }

        /// <summary>Zero where the face has no bar, in which case the row is not drawn at all.</summary>
        public double Bar { get; private set; }

        /// <summary>The whole region the three zones share, the gaps between them included.</summary>
        public double Body { get; private set; }

        public double Band { get; private set; }

        /// <summary>The zone letters of the body, in the order the picture draws them.</summary>
        public string[] Letters { get; private set; }

        /// <summary>The cells of the body along the axis it lays them out: widths across a wide face,
        /// heights down a portrait one.</summary>
        public double[] Cells { get; private set; }

        /// <summary>The rows and the gaps between them, which is what a page has to find room for inside the
        /// inset.</summary>
        public double Height
        {
            get { return RevBar + Seam + (HasBar ? Bar + Seam : 0) + Body + Seam + Band; }
        }

        /// <summary>"Zone B", and "Band D" for the zone that is drawn as a band. Presentation only: the
        /// letter D is what every setting, every property and every action is keyed on.</summary>
        public static string ZoneLabel(string letter)
        {
            return letter == "D" ? "Band D" : "Zone " + letter;
        }

        /// <summary>
        /// The zones in the order the panel shows them: the body's own order, band D after it.
        /// </summary>
        /// <remarks>
        /// Contract.FaceZoneLetters keeps its A, B, C, D, because it indexes the settings arrays and a
        /// panel's reading order must not reach into them. This order is a permutation of it and
        /// PanelFacePlanTests holds it to that, so a letter cannot be dropped from the panel while the
        /// setting behind it goes on existing.
        /// </remarks>
        public static string[] ZoneOrder(Contract.FaceSize face)
        {
            var body = face.BodyOrder;
            var order = new string[body.Length + 1];
            Array.Copy(body, order, body.Length);
            order[body.Length] = "D";
            return order;
        }

        /// <summary>Every zone and page the quick glance can be set to, packed the way
        /// Contract.QuickGlanceValue packs them, in Contract.FaceZoneLetters order.</summary>
        public static int[] GlanceOptions()
        {
            var values = new List<int>();
            for (var zone = 0; zone < Contract.FaceZoneLetters.Length; zone++)
            {
                var pages = FacePages.CountAt(zone);
                for (var page = 0; page < pages; page++) values.Add(Contract.QuickGlanceValue(zone, page));
            }
            return values.ToArray();
        }

        /// <summary>"Zone C · Track": the zone and the page together, as a glance is one choice.</summary>
        public static string GlanceLabel(int value)
        {
            var glance = Contract.NormaliseQuickGlance(value);
            var letter = Contract.FaceZoneLetters[Contract.QuickGlanceZone(glance)];
            return ZoneLabel(letter) + " · " + FacePages.NameOf(letter, Contract.QuickGlancePage(glance));
        }

        /// <summary>A row of the picture: the face's own rectangle at the picture's scale, floored so that
        /// a row never takes a pixel the face does not give it, and never below what it has to hold.</summary>
        private static double Row(int rect, double scale, double least)
        {
            return Math.Max(least, Math.Floor(rect * scale));
        }

        /// <summary>
        /// The three cells of the body, from the face's own parts.
        /// </summary>
        /// <remarks>
        /// Every cell but the last is rounded and the last takes what is left, so that the cells and
        /// the gaps between them add up to the span exactly rather than leaving a sliver of ground showing
        /// at the end of the row.
        /// </remarks>
        private static double[] Split(double span, int[] parts)
        {
            var usable = span - (parts.Length - 1) * Seam;
            var total = 0;
            foreach (var part in parts) total += part;
            var sizes = new double[parts.Length];
            var taken = 0.0;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                sizes[i] = Math.Round(usable * parts[i] / total);
                taken += sizes[i];
            }
            sizes[parts.Length - 1] = usable - taken;
            return sizes;
        }
    }
}
