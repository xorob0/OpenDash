// PanelSoon.cs: every control the redesign draws that is not built yet, with the ticket that builds it.
//
// The rule #503 sets for the artboards is that every control either works or is drawn greyed with its
// ticket in the hover, so a driver sees where the panel is going and a contributor finds the issue from the
// panel. One registry rather than a ticket number typed beside each row, so PanelSoonTests can hold every
// number to the set of open tickets this was checked against, and a greyed row cannot cite a ticket that
// closed (#370, #145 and #487 did; #322 shipped, so its rows are live).
//
// Search lists every entry a page draws (PanelSearch.All), so somebody looking for a theme finds that it is
// coming once the Settings page draws its row. An entry no page draws yet is left out of search, since a
// result that opens a page with no such row is worse than none; PanelSoonTests reads the pages' sources and
// holds DrawnOnPages and DrawnOneByOne to what they draw, so a page agent that draws its rows moves them into
// search by adding its page here.
//
// Pure: no WPF. Ui.Soon draws an entry.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One control that is drawn and not yet built.</summary>
    public sealed class SoonItem
    {
        public SoonItem(int ticket, string title, PanelPage page)
        {
            Ticket = ticket;
            Title = title;
            Page = page;
        }

        public int Ticket { get; private set; }

        /// <summary>The row's label, as a noun phrase (docs/design/voice.md).</summary>
        public string Title { get; private set; }

        public PanelPage Page { get; private set; }

        public string Tip { get { return PanelSoon.Tip(Ticket); } }

        /// <summary>
        /// The anchor the greyed row carries (Ui.Soon puts it there), which a search hit scrolls to:
        /// "soon.", the ticket and the title's slug, so two rows of one ticket are two anchors.
        /// </summary>
        public string Anchor { get { return "soon." + Ticket.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + Contract.Slug(Title).ToLowerInvariant(); } }
    }

    public static class PanelSoon
    {
        /// <summary>The tag a greyed row carries after its title.</summary>
        public const string Tag = "Soon";

        /// <summary>The tag a control new in this release carries after its title.</summary>
        public const string NewTag = "New";

        /// <summary>The hover of a greyed row: "Coming soon · #381".</summary>
        public static string Tip(int ticket)
        {
            return "Coming soon · #" + ticket.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static readonly IReadOnlyList<SoonItem> All = new[]
        {
            // Screens: a face's own extras, the pages the zones do not have yet, and the round face's zones.
            new SoonItem(381, "Rev fill under the lights", PanelPage.Screens),
            new SoonItem(96, "Spotter at the rev bar ends", PanelPage.Screens),
            new SoonItem(383, "Pit page in the pit lane", PanelPage.Screens),
            new SoonItem(112, "Pop-ups", PanelPage.Screens),
            new SoonItem(321, "Edge lights for the delta", PanelPage.Screens),
            new SoonItem(114, "Screen care", PanelPage.Screens),
            new SoonItem(319, "Fit", PanelPage.Screens),
            new SoonItem(320, "Circle tracker", PanelPage.Screens),
            new SoonItem(152, "Launch", PanelPage.Screens),
            new SoonItem(116, "Flags screen", PanelPage.Screens),
            new SoonItem(85, "Your displays", PanelPage.Screens),
            new SoonItem(146, "Zones instead of cards", PanelPage.Screens),

            // LEDs.
            new SoonItem(434, "Each LED in turn", PanelPage.Leds),
            new SoonItem(485, "Idle sweep", PanelPage.Leds),
            new SoonItem(300, "Engine start animation", PanelPage.Leds),
            new SoonItem(479, "Car data for AC, ACC and LMU", PanelPage.Leds),
            new SoonItem(509, "Pit limiter lights", PanelPage.Leds),

            // Matrix.
            new SoonItem(371, "RPM colour for everything", PanelPage.Matrix),
            new SoonItem(363, "SimHub device", PanelPage.Matrix),
            new SoonItem(505, "Priority order", PanelPage.Matrix),

            // Rig.
            new SoonItem(506, "Real hardware", PanelPage.Rig),

            // Shortcuts.
            new SoonItem(511, "Rig test", PanelPage.Shortcuts),
            new SoonItem(510, "Alert dismissal", PanelPage.Shortcuts),

            // Settings.
            new SoonItem(326, "Fuel target per lap", PanelPage.Settings),
            new SoonItem(325, "Tyre display", PanelPage.Settings),
            new SoonItem(504, "Yellow flags", PanelPage.Settings),
            new SoonItem(507, "Tyre wear", PanelPage.Settings),
            new SoonItem(507, "Pit window open", PanelPage.Settings),
            new SoonItem(508, "Incidents", PanelPage.Settings),
            new SoonItem(110, "Hybrid battery low", PanelPage.Settings),
            new SoonItem(512, "Alert display", PanelPage.Settings),
            new SoonItem(128, "Sim time of day", PanelPage.Settings),
            new SoonItem(128, "Screen dimming", PanelPage.Settings),
            new SoonItem(99, "Theme", PanelPage.Settings),
            new SoonItem(129, "Colour vision", PanelPage.Settings),
            new SoonItem(127, "Colours", PanelPage.Settings),
            new SoonItem(484, "Name", PanelPage.Settings),
            new SoonItem(484, "Race number", PanelPage.Settings),
            new SoonItem(484, "Logo", PanelPage.Settings),
            new SoonItem(104, "Idle screen background", PanelPage.Settings),
        };

        /// <summary>The pages whose build draws every greyed row the registry gives them (<see cref="For"/>).</summary>
        public static readonly IReadOnlyList<PanelPage> DrawnOnPages = new[] { PanelPage.Shortcuts };

        /// <summary>The greyed rows a page draws one at a time (<see cref="Find"/>), by title.</summary>
        public static readonly IReadOnlyList<string> DrawnOneByOne = new[] { PanelRigMap.RealHardwareTitle };

        /// <summary>Whether a page draws this greyed row, which is whether search may send a driver to it.</summary>
        public static bool IsDrawn(SoonItem item)
        {
            return item != null && (DrawnOnPages.Contains(item.Page) || DrawnOneByOne.Contains(item.Title, StringComparer.Ordinal));
        }

        /// <summary>Every greyed control on one page, in the order the page draws them.</summary>
        public static IEnumerable<SoonItem> For(PanelPage page)
        {
            return All.Where(item => item.Page == page);
        }

        /// <summary>The entry of that title, or null. Ordinal, since a title is pinned text.</summary>
        public static SoonItem Find(string title)
        {
            return All.FirstOrDefault(item => string.Equals(item.Title, title, StringComparison.Ordinal));
        }

        /// <summary>Every ticket the registry cites, once each.</summary>
        public static IEnumerable<int> Tickets()
        {
            return All.Select(item => item.Ticket).Distinct();
        }
    }
}
