// PanelPitWallPlan.cs: the picture of the pit wall page the Screens page configures a screen on, the
// words that go beside it, and the companion grid, as numbers.
//
// A pit wall's zones are letters, and a letter is a position: zone C is wherever the Tower page draws it.
// The panel therefore says where each zone is twice, once as a miniature of every page and once as a
// sentence in the zone's own row, and until this class the two disagreed. The miniature stacked C above D
// and drew the wide zone down the left, while the sentence beside it said C was the lower left of the
// tower and D the lower right; the Tower page's fixed panel was not drawn at all. Both halves now read the
// table below, so a rectangle that moves takes its sentence with it and PanelPitWallPlanTests fails when
// it does not.
//
// Screens.dc.html draws one page at a time, the one "Page on screen" picks, at the wall's own 16 by 9 and
// as wide as the column gives it; the table below is that page at 253 across, and Scaled draws it at any
// other width.
//
// Apart from SettingsControl.Screens.PitWall.cs for the reason PanelFacePlan.cs is apart from it: the panel is WPF
// and the net8.0 test project cannot compile a line of it, so the geometry lives where the tests can hold
// it against the canvas. Pure: no WPF types.
//
// PanelCompanionPlan is at the foot of the same file because a companion carries three numbers and no
// geometry at all, which is too little to be a file of its own; it is here rather than in PanelMetrics.cs
// only because the two panes were rebuilt together.
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>The three pit wall pages as rectangles, and the sentence each zone's row carries.</summary>
    public static class PanelPitWallPlan
    {
        /// <summary>The design space the page table is written in: 1920 x 1080 at about 0.132. A picture at
        /// another width is this scaled, never a rectangle near it.</summary>
        public const double ThumbWidth = 253;

        public const double ThumbHeight = 142;

        /// <summary>Every panel is drawn this far in from the page's edge, which is what leaves the
        /// surface.inset ground showing around them.</summary>
        public const double Inset = 4;

        /// <summary>The zone list beside the picture (the artboard's 300 px card), and the gap between them.</summary>
        public const double ListWidth = 300;

        public const double ListGap = 24;

        /// <summary>The rule around the picture, inside the width the column gives it.</summary>
        public const double Frame = 1;

        /// <summary>The narrowest the picture is drawn, however narrow the column.</summary>
        public const double PictureLeast = 120;

        /// <summary>
        /// The width the picture takes on a page <paramref name="content"/> wide, its frame included: the
        /// content less the zone list and its gap in two columns, the content stacked, and in either case no
        /// wider than <see cref="StackedMax"/>.
        /// </summary>
        /// <remarks>
        /// A 16 by 9 picture the width of a wide page stood 485 px tall and pushed the rows under it out of
        /// sight, which the face's picture is capped against for the same reason. Beside the list it used to
        /// stop only at the content's 1112 px ceiling; the column has none now, and at a 3840 px window the
        /// picture was 3195 px wide and 1796 tall. Past the cap the zone list beside it takes the room.
        /// </remarks>
        public static double PictureWidthFor(double content, bool twoColumns)
        {
            var column = twoColumns ? content - ListWidth - ListGap : content;
            return Math.Max(PictureLeast, Math.Min(column, StackedMax));
        }

        /// <summary>The widest a stacked picture is drawn, frame included: the widest whose page
        /// (<see cref="PictureHeight"/>) stands no taller than PanelFacePlan.MaxHeight.</summary>
        public static readonly double StackedMax = Math.Ceiling((PanelFacePlan.MaxHeight + 1) * 16 / 9) - 1 + 2 * Frame;

        /// <summary>The page itself inside a picture <paramref name="outer"/> wide: the frame is drawn inside
        /// the column, not beside it, or the column clips the picture's right edge.</summary>
        public static double CanvasWidth(double outer)
        {
            return outer - 2 * Frame;
        }

        /// <summary>A panel of the picture: its name at 12 over what it shows at 14, padded 8 by 6. The
        /// artboard writes a zone's bare letter at 16 in the display face; the page writes "Zone A" and
        /// "Wide zone", which at 16 does not fit a Tower zone on a narrow page.</summary>
        public const double ZoneNameSize = Theme.SizeLabel;

        public const double ZonePageSize = Theme.SizeBody;

        public const double ZonePaddingX = 8;

        public const double ZonePaddingY = 6;

        /// <summary>The web view address box, as the artboard draws it.</summary>
        public const double AddressWidth = 320;

        /// <summary>The address box in a column <paramref name="column"/> wide: the artboard's 320, or less
        /// where 320 would squeeze the row's title under PanelScreens.RowTitleLeast, as every other row's
        /// controls are held.</summary>
        public static double AddressWidthFor(double column)
        {
            return Math.Min(AddressWidth, PanelScreens.ControlsWidth(column));
        }

        /// <summary>The picture's height at a width: the wall's own 16 by 9.</summary>
        public static double PictureHeight(double width)
        {
            return Math.Floor(width * 9 / 16);
        }

        /// <summary>A page's panels at a picture <paramref name="width"/> across, scaled from the table.</summary>
        public static IReadOnlyList<Panel> Scaled(Page page, double width)
        {
            var panels = new List<Panel>();
            if (page == null) return panels;
            var k = width / ThumbWidth;
            foreach (var panel in page.Panels)
            {
                panels.Add(new Panel(panel.Name, Math.Round(panel.X * k), Math.Round(panel.Y * k), Math.Floor(panel.Width * k), Math.Floor(panel.Height * k), panel.Configurable, panel.Shows));
            }
            return panels;
        }

        /// <summary>What an empty address box shows. A watermark and never a value: Contract.NormaliseUrl
        /// keeps only an absolute address, so a bare scheme stored in the setting is blanked on the first
        /// commit and the box would empty itself in front of the user.</summary>
        public const string AddressPlaceholder = "https://";

        /// <summary>One panel of one page: where it is drawn, and whether the letter on it is a zone the
        /// list below can be pointed at or a fixed part of the page.</summary>
        public sealed class Panel
        {
            public Panel(string name, double x, double y, double width, double height, bool configurable, string shows = null)
            {
                Name = name;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Configurable = configurable;
                Shows = shows;
            }

            /// <summary>What a fixed panel shows, drawn under its name as a zone's page is ("Board ·
            /// Leaderboard" on the artboard's Race picture); null for a zone, whose page is its setting.</summary>
            public string Shows { get; private set; }

            public string Name { get; private set; }

            public double X { get; private set; }

            public double Y { get; private set; }

            public double Width { get; private set; }

            public double Height { get; private set; }

            /// <summary>True for a letter and for the wide zone, false for the board and the tower, which
            /// is what decides whether the name is drawn in the accent or in text.label.</summary>
            public bool Configurable { get; private set; }

            public double Right { get { return X + Width; } }

            public double Bottom { get { return Y + Height; } }

            public double CentreX { get { return X + Width / 2; } }

            public double CentreY { get { return Y + Height / 2; } }
        }

        /// <summary>One page of the pit wall, named as the page picker names it.</summary>
        public sealed class Page
        {
            public Page(string title, params Panel[] panels)
            {
                Title = title;
                Panels = panels;
            }

            public string Title { get; private set; }

            public IReadOnlyList<Panel> Panels { get; private set; }
        }

        /// <summary>Where one zone is drawn, as the row beside the picture says it.</summary>
        public sealed class ZonePlace
        {
            public ZonePlace(string page, string where)
            {
                Page = page;
                Where = where;
            }

            /// <summary>The title of the page this is a place on, which is one of the three below.</summary>
            public string Page { get; private set; }

            /// <summary>"upper right", "top", "lower left": the words the sentence ends on, and what the
            /// test holds the rectangle to.</summary>
            public string Where { get; private set; }
        }

        /// <summary>
        /// The three pages in the order the picture draws them.
        /// </summary>
        /// <remarks>
        /// The Tower page is the one the old two-column helper could not express, and it is the reason the
        /// whole picture is a table: a fixed panel down the left, a wide zone across the top of the right
        /// and two zones side by side under it is four rectangles in three regions, which no arrangement
        /// of two star columns produces.
        /// </remarks>
        public static readonly IReadOnlyList<Page> Pages = new[]
        {
            new Page("Race",
                new Panel("Board", 4, 4, 118, 134, false, "Leaderboard"),
                new Panel("A", 128, 4, 118, 65, true),
                new Panel("B", 128, 73, 118, 65, true)),
            new Page("Tower",
                new Panel("Tower", 4, 4, 110, 134, false, "Leaderboard"),
                new Panel("Wide", 118, 4, 131, 59, true),
                new Panel("A", 118, 67, 63, 71, true),
                new Panel("B", 185, 67, 63, 71, true)),
            new Page("Telemetry",
                new Panel("A", 4, 4, 243, 39, true),
                new Panel("B", 4, 49, 243, 41, true),
                new Panel("C", 4, 96, 243, 39, true)),
        };

        /// <summary>
        /// Where on its own page each zone sits.
        /// </summary>
        /// <remarks>
        /// One place each, now that a zone belongs to a page. The table used to give A two places -- the
        /// race page's upper right and the telemetry page's top -- because it was one setting drawn in two
        /// pages, and the caption saying so was the only warning a driver got that moving one moved both.
        /// </remarks>
        private static readonly Dictionary<string, string> ZoneWhere = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "RaceA", "upper right" },
            { "RaceB", "lower right" },
            { "TowerWide", "across the top of the right column" },
            { "TowerA", "lower left" },
            { "TowerB", "lower right" },
            { "TelemetryA", "top" },
            { "TelemetryB", "middle" },
            { "TelemetryC", "bottom" },
            { "PortraitA", "upper left" },
            { "PortraitB", "upper right" },
            { "PortraitC", "lower left" },
            { "PortraitD", "lower right" },
        };

        /// <summary>Where one zone is: "Race page, upper right." Written from the table above rather than
        /// beside it, so the words cannot drift from the picture the panel draws; PanelPitWallPlanTests reads
        /// each one off its rectangle. The landscape zones are not described on the page, which draws them in
        /// the picture beside their list.</summary>
        public static string ZoneDescription(Contract.PitWallZoneSlot slot)
        {
            if (slot == null) return string.Empty;
            string where;
            if (!ZoneWhere.TryGetValue(slot.Key, out where)) return string.Empty;
            return slot.Page + " page, " + where + ".";
        }

        /// <summary>A portrait zone's hover: its place alone, "Upper left.", under a row titled Portrait layout,
        /// where "Portrait page" would name a page the wall does not have.</summary>
        public static string ZonePosition(Contract.PitWallZoneSlot slot)
        {
            string where;
            if (slot == null || !ZoneWhere.TryGetValue(slot.Key, out where) || where.Length == 0) return string.Empty;
            return char.ToUpperInvariant(where[0]) + where.Substring(1) + ".";
        }

        /// <summary>Every zone and page the quick glance can be set to, packed the way
        /// Contract.PitWallQuickGlanceValue packs them, in GlanceZones order.</summary>
        public static int[] GlanceOptions()
        {
            var values = new List<int>();
            var zones = Contract.GlanceZoneSlots();
            for (var zone = 0; zone < zones.Count; zone++)
            {
                for (var page = 0; page < ZonePages.Standard.Count; page++) values.Add(Contract.PitWallQuickGlanceValue(zone, page));
            }
            return values.ToArray();
        }

        /// <summary>"Tower A · Relative": one control names the zone and the page together, because a
        /// glance is one choice and the two halves of it mean nothing apart. The page's name is in it now
        /// that a zone belongs to one, or two of the eight would read the same.</summary>
        public static string GlanceLabel(int value)
        {
            var glance = Contract.NormalisePitWallQuickGlance(value);
            var zones = Contract.GlanceZoneSlots();
            var index = Contract.QuickGlanceZone(glance);
            var slot = index >= 0 && index < zones.Count ? zones[index] : zones[0];
            return slot.Page + " " + slot.Slot + " · " + ZonePages.StandardName(Contract.QuickGlancePage(glance));
        }

        /// <summary>The page of that title, or null. Used by the tests to hold a sentence against the
        /// rectangles of the page it names.</summary>
        public static Page PageNamed(string title)
        {
            foreach (var page in Pages)
            {
                if (string.Equals(page.Title, title, StringComparison.Ordinal)) return page;
            }
            return null;
        }
    }

    /// <summary>The round screen's picture: the disc and the cards it carries, as Screens.dc.html draws it.</summary>
    public static class PanelRoundPlan
    {
        /// <summary>The artboard's 240 px disc, and the 32 between it and the rows beside it.</summary>
        public const double PictureSize = 240;

        public const double PictureGap = 32;

        /// <summary>Between two cards on the disc, across and down.</summary>
        public const double CardGap = 6;

        /// <summary>A card is padded 6 at the sides and 7 above and below.</summary>
        public const double CardPaddingX = 6;

        public const double CardPaddingY = 7;

        /// <summary>Between a card's label and the card it shows.</summary>
        public const double CardLineGap = 4;

        /// <summary>A card's height: its rule, its padding, the 11 px label and the 14 px card name at
        /// PanelFacePlan.LineHeight, and the gap between them.</summary>
        public const double CardHeight = 50;

        /// <summary>The most cards the disc carries: the 800 round's six. A card face of another size reads
        /// all twelve slots, which stand taller than the disc in any arrangement of these cards, so it is
        /// drawn as its rows alone.</summary>
        public const int MostOnDisc = 6;

        /// <summary>Whether the disc is drawn with the cards on it.</summary>
        public static bool OnDisc(int read)
        {
            return read > 0 && read <= MostOnDisc;
        }

        /// <summary>The cards stand in one column for two, and in two columns of three for six.</summary>
        public static int Columns(int read)
        {
            return read > 2 ? 2 : 1;
        }

        /// <summary>A card's width: 140 alone in its column, 84 beside another, which keeps every corner of
        /// the three rows of two inside the disc (88 put the top row's corners 2 px past it).</summary>
        public static double CardWidth(int columns)
        {
            return columns == 1 ? 140 : 84;
        }
    }

    /// <summary>The companion's grid of modules, which has no picture and so no plan beyond a few numbers.</summary>
    public static class PanelCompanionPlan
    {
        /// <summary>Three columns of seven, as Screens.dc.html draws them: a checkbox beside each name, which
        /// leaves the room a switch took.</summary>
        public const int ModuleColumns = 3;

        /// <summary>Between two modules, across and down: the artboard's 6.</summary>
        public const double ModuleGap = 6;

        /// <summary>One module's row (.mod): 34 high, padded 10.</summary>
        public const double ModuleHeight = 34;

        public const double ModulePaddingX = 10;

        /// <summary>"Not in iRacing" beside a module's name, smaller than the name: the artboard's 11.</summary>
        public const double NoteSize = 11;

        /// <summary>The narrowest a column may be and still hold a name and "Not in iRacing" beside it.</summary>
        public const double ModuleLeast = 200;

        /// <summary>How many columns fit a grid <paramref name="width"/> across: three at most, one at least.</summary>
        public static int ColumnsFor(double width)
        {
            for (var columns = ModuleColumns; columns > 1; columns--)
            {
                if (columns * ModuleLeast + (columns - 1) * ModuleGap <= width) return columns;
            }
            return 1;
        }
    }
}
