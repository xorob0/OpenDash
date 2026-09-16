// PanelFacePlan.cs: the picture of a face the Rig tab configures a screen on, as numbers.
//
// A screen is configured on a picture of itself, because "zone C" means nothing until you see where
// zone C is. The picture is the face's own rectangles scaled to the width the panel gives it, which is
// what stops one face's proportions being drawn for every face; before this class the four rows were
// four constants, so a 1920 x 480 face whose rows are 48, 56, 314 and 60 was drawn 19, 24, 150 and 26.
//
// Apart from SettingsControl.Panes.cs for the reason PanelMetrics.cs is apart from Widgets.cs: the panel
// is WPF and the net8.0 test project cannot compile a line of it, so the geometry lives where
// PanelFacePlanTests can hold it against the canvas. Pure: no WPF types.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>The rows and the cells of one face's picture, at the width the panel draws it.</summary>
    public sealed class PanelFacePlan
    {
        /// <summary>The width every face is drawn at, which is the canvas's own number inside the 896 px
        /// the body of a pane is given.</summary>
        public const double PictureWidth = 844;

        /// <summary>The one pixel of ui.rule between two rows of the picture, and between two cells.</summary>
        public const double Seam = 1;

        // The least a row can be and still hold what it draws. A row of the picture is a strip of
        // controls rather than a band of pixels, so where a face's own rectangle scales to less than its
        // controls need, the row keeps the controls and stops being to scale. At 1920 x 480 this is what
        // draws the rev bar at 26 and band D at 30 where the face alone would give 21 and 26.
        public const double RevBarLeast = 26;
        public const double BarLeast = Theme.ControlHeightSm;
        public const double BandLeast = 30;

        /// <summary>
        /// The least a zone cell can be: its padding above and below, its letter, and the two controls
        /// under it nine apart.
        /// </summary>
        /// <remarks>
        /// The tightest box on the panel, which is why it is written down rather than left to come out
        /// of the arithmetic. The reference face scales to exactly this, so the floor binds on no face
        /// that ships today and is here for the one that does not fit tomorrow.
        /// </remarks>
        public const double CellLeast = 138;

        // The controls inside the picture. Canvas geometry the token file does not carry; docs/design/plugin.md
        // records which of these are owed as tokens, in the way PanelMetrics.cs records its own.

        /// <summary>A zone cell is padded 8 above and below and 7 at the sides, which is what makes zone B's
        /// select 324 wide inside a cell of 338.</summary>
        public const double CellPaddingX = 7;

        public const double CellPaddingY = 8;

        /// <summary>Between the letter, the page select and the count, and between the class filter and
        /// the control above it.</summary>
        public const double CellGap = 9;

        /// <summary>The two ends of the bar, which are not the same width: the right end carries a class
        /// position beside a position and needs the room.</summary>
        public const double BarEndLeftWidth = 132;

        public const double BarEndRightWidth = 146;

        /// <summary>Band D lays its controls along the row rather than stacking them, so it carries a gap
        /// of its own and a select narrower than the band is wide.</summary>
        public const double BandGap = 10;

        public const double BandSelectWidth = 150;

        /// <summary>The count of a cycle is a tracked label rather than a value: eleven, in text.label,
        /// under a chevron of twelve rather than the sixteen every other icon is drawn at.</summary>
        public const double CountCaptionSize = 11;

        public const double CountChevronSize = 12;

        /// <summary>Between one wheel binding and the next, across and down: they wrap into two columns
        /// rather than stacking into four rows.</summary>
        public const double BindingGap = 22;

        /// <summary>The glance is one choice and not two, so it is one select, with its binder beside it
        /// rather than under a label of its own.</summary>
        public const double GlanceSelectWidth = 200;

        public const double GlanceBinderGap = 12;

        private PanelFacePlan(Contract.FaceSize face)
        {
            var scale = PictureWidth / face.Width;
            Stacked = face.Body == Contract.FaceBody.Column;
            HasBar = face.HasBar;
            RevBar = Row(face.RevBarHeight, scale, RevBarLeast);
            Bar = face.HasBar ? Row(face.BarHeight, scale, BarLeast) : 0;
            var cells = face.Parts.Length;
            Body = Row(face.BodyHeight, scale, Stacked ? cells * CellLeast + (cells - 1) * Seam : CellLeast);
            Band = Row(face.BandHeight, scale, BandLeast);
            Letters = face.BodyOrder;
            Cells = Split(Stacked ? Body : PictureWidth, face.Parts);
        }

        /// <summary>The picture of one face.</summary>
        public static PanelFacePlan For(Contract.FaceSize face)
        {
            return new PanelFacePlan(face);
        }

        /// <summary>Zone A over zone B over zone C, rather than side by side.</summary>
        public bool Stacked { get; private set; }

        /// <summary>False on the nano, whose picture has three rows and not four.</summary>
        public bool HasBar { get; private set; }

        public double RevBar { get; private set; }

        /// <summary>Zero where the face has no bar, in which case the row is not drawn at all.</summary>
        public double Bar { get; private set; }

        /// <summary>The whole region the three zones share, the seams between them included.</summary>
        public double Body { get; private set; }

        public double Band { get; private set; }

        /// <summary>The zone letters of the body, in the order the picture draws them.</summary>
        public string[] Letters { get; private set; }

        /// <summary>The cells of the body along the axis it lays them out: widths across a wide face,
        /// heights down a portrait one.</summary>
        public double[] Cells { get; private set; }

        /// <summary>The whole picture, seams included, which is what a pane has to find room for.</summary>
        public double Height
        {
            get { return RevBar + Seam + (HasBar ? Bar + Seam : 0) + Body + Seam + Band; }
        }

        /// <summary>What a control inside a cell is given, which is the cell less its side padding.</summary>
        public static double Inner(double cell)
        {
            return cell - 2 * CellPaddingX;
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

        /// <summary>"Zone C · Track": one control names the zone and the page together, because a glance
        /// is one choice and the two halves of it mean nothing apart.</summary>
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
        /// their seams add up to the span exactly rather than leaving a pixel of rule showing at the end
        /// of the row. At 1920 x 480 that is 338, 167 and 337.
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
