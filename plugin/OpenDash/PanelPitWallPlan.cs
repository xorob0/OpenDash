// PanelPitWallPlan.cs: the picture of the three pit wall pages the Rig tab configures a screen on, the
// words that go under it, and the companion grid beside it, as numbers.
//
// A pit wall's zones are letters, and a letter is a position: zone C is wherever the Tower page draws it.
// The panel therefore says where each zone is twice, once as a miniature of every page and once as a
// sentence in the zone's own row, and until this class the two disagreed. The miniature stacked C above D
// and drew the wide zone down the left, while the sentence beside it said C was the lower left of the
// tower and D the lower right; the Tower page's fixed panel was not drawn at all. Both halves now read the
// table below, so a rectangle that moves takes its sentence with it and PanelPitWallPlanTests fails when
// it does not.
//
// Apart from SettingsControl.Panes.cs for the reason PanelFacePlan.cs is apart from it: the panel is WPF
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
        /// <summary>A page miniature, which is 1920 x 1080 at about 0.132. The pit wall is the one kind
        /// whose picture is three pictures, so the scale is the canvas's own number rather than a width
        /// divided by the face's, the way PanelFacePlan.PictureWidth is.</summary>
        public const double ThumbWidth = 253;

        public const double ThumbHeight = 142;

        /// <summary>Between one miniature and the next. Three of them and two gaps come to 797 with the
        /// one pixel frame each carries, which is what has to fit the 896 a pane's body is given.</summary>
        public const double ThumbGap = 16;

        /// <summary>Every panel is drawn this far in from the miniature's edge, which is what leaves the
        /// surface.inset ground showing around them rather than a page of pure ui.field.</summary>
        public const double Inset = 4;

        /// <summary>The page's name under its miniature: text.secondary at the caption size, not the
        /// tracked label the miniature's own panels are drawn with.</summary>
        public const double CaptionSize = Theme.SizeSmall;

        public const double CaptionGap = 8;

        /// <summary>Between one zone row and the next, which is wider than the twelve a pane's own rows
        /// take because a zone row is a title over a caption rather than a single line.</summary>
        public const double RowGap = 14;

        /// <summary>Every control in the list is the same box: the four zones, the wide zone and the web
        /// view address, at the full control height and so at the body size rather than the label size.
        ///
        /// It was 220, which is what the face pane's glance select takes. The wide zone names three of
        /// its six pages by what the extra width buys, and "Lap history · delta to best" is longer than
        /// anything the narrow list holds, so the box grew with the longest label rather than letting a
        /// ComboBox ellipsise the half of the name that says which page it is.</summary>
        public const double SelectWidth = 260;

        public const double SelectHeight = Theme.ControlHeight;

        public const double AddressWidth = SelectWidth;

        /// <summary>What an empty address box shows. A watermark and never a value: Contract.NormaliseUrl
        /// keeps only an absolute address, so a bare scheme stored in the setting is blanked on the first
        /// commit and the box would empty itself in front of the user.</summary>
        public const string AddressPlaceholder = "https://";

        /// <summary>One panel of one page: where it is drawn, and whether the letter on it is a zone the
        /// list below can be pointed at or a fixed part of the page.</summary>
        public sealed class Panel
        {
            public Panel(string name, double x, double y, double width, double height, bool configurable)
            {
                Name = name;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Configurable = configurable;
            }

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
                new Panel("Board", 4, 4, 118, 134, false),
                new Panel("A", 128, 4, 118, 65, true),
                new Panel("B", 128, 73, 118, 65, true)),
            new Page("Tower",
                new Panel("Tower", 4, 4, 110, 134, false),
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

        /// <summary>The caption of one zone's row: "Race page, upper right." Written from the table above
        /// rather than beside it, so the words cannot drift from the picture the panel draws.</summary>
        public static string ZoneDescription(Contract.PitWallZoneSlot slot)
        {
            if (slot == null) return string.Empty;
            string where;
            if (!ZoneWhere.TryGetValue(slot.Key, out where)) return string.Empty;
            return slot.Page + " page, " + where + ".";
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

    /// <summary>The companion's grid of modules, which has no picture and so no plan beyond three numbers.</summary>
    public static class PanelCompanionPlan
    {
        /// <summary>Two columns of eleven and ten rather than three of seven: the name is a body line and
        /// not a label, and three columns of it do not leave room for the toggle at the panel's width.</summary>
        public const int ModuleColumns = 2;

        public const double ModuleRowGap = 12;

        public const double ModuleColumnGap = 40;
    }
}
