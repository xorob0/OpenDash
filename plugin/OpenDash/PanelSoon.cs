// PanelSoon.cs: every control the redesign draws that is not built yet, with the ticket that builds it.
//
// The rule #791 sets for the artboards is that every control either works or is drawn greyed with its
// ticket in the hover, so a driver sees where the panel is going and a contributor finds the issue from the
// panel. One registry rather than a ticket number typed beside each row, so PanelSoonTests can hold every
// number to the set of open tickets this was checked against, and a greyed row cannot cite a ticket that
// closed (#370, #145 and #487 did; #322 shipped, so its rows are live).
//
// Each page says which greyed rows it draws, in a SoonDrawn list in its own Panel<Page>.cs, and search lists
// those rows (PanelSearch.Soon): somebody looking for a theme finds that it is coming once the Settings page
// draws its row, and an entry no page draws is left out, since a result that opens a page with no such row is
// worse than none. PanelSoonTests holds each page's list to that page's own sources, so a page agent that
// draws a row adds it to its own list and touches no shell file. A row drawn only inside a sheet (the Add
// screen sheet's #739 and #85 tiles) is marked InSheetOnly: it may be drawn, and search never lists it, since
// search cannot open the sheet.
//
// A page draws an entry by name -- Ui.Soon(row, PanelSoon.RevFill), Ui.SoonRow(PanelSoon.DashTheme) -- or all
// of its page's entries through For(page). Nothing but this file constructs a SoonItem, no other file writes
// the "Coming soon" hover, and only this file and the kit call Tip(ticket) or draw the bare Soon tag, which
// PanelSoonTests holds, so a ticket cannot be typed beside a row. No shell partial draws an entry: a row
// belongs to a page, whose SoonDrawn lists it.
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
        /// <summary>Internal, and constructed only in PanelSoon.cs (PanelSoonTests holds that), so a page cannot
        /// grey a row with a ticket typed beside it.</summary>
        internal SoonItem(int ticket, string title, PanelPage page, bool inSheetOnly = false)
        {
            Ticket = ticket;
            Title = title;
            Page = page;
            InSheetOnly = inSheetOnly;
        }

        /// <summary>Drawn only inside a sheet, which search cannot open, so never listed by search.</summary>
        public bool InSheetOnly { get; private set; }

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

        /// <summary>The hover of a greyed row: "Coming soon · #746".</summary>
        public static string Tip(int ticket)
        {
            return "Coming soon · #" + ticket.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // Screens: a face's own extras, the pages the zones do not have yet, and the round face's zones.
        public static readonly SoonItem RevFill = new SoonItem(746, "Rev fill under the lights", PanelPage.Screens);
        public static readonly SoonItem SpotterAtRevBarEnds = new SoonItem(96, "Spotter at the rev bar ends", PanelPage.Screens);
        public static readonly SoonItem PitPageInPitLane = new SoonItem(383, "Pit page in the pit lane", PanelPage.Screens);
        public static readonly SoonItem PopUps = new SoonItem(112, "Pop-ups", PanelPage.Screens);
        public static readonly SoonItem DeltaEdgeLights = new SoonItem(321, "Edge lights for the delta", PanelPage.Screens);
        public static readonly SoonItem ScreenCare = new SoonItem(114, "Screen care", PanelPage.Screens);
        public static readonly SoonItem Fit = new SoonItem(319, "Fit", PanelPage.Screens);
        public static readonly SoonItem CircleTracker = new SoonItem(320, "Circle tracker", PanelPage.Screens);
        public static readonly SoonItem Launch = new SoonItem(741, "Launch", PanelPage.Screens);
        public static readonly SoonItem FlagsScreen = new SoonItem(739, "Flags screen", PanelPage.Screens, inSheetOnly: true);
        public static readonly SoonItem YourDisplays = new SoonItem(85, "Your displays", PanelPage.Screens, inSheetOnly: true);
        public static readonly SoonItem ZonesInsteadOfCards = new SoonItem(146, "Zones instead of cards", PanelPage.Screens);

        // LEDs.
        public static readonly SoonItem EachLedInTurn = new SoonItem(747, "Each LED in turn", PanelPage.Leds);
        public static readonly SoonItem IdleSweep = new SoonItem(485, "Idle sweep", PanelPage.Leds);
        public static readonly SoonItem EngineStartAnimation = new SoonItem(743, "Engine start animation", PanelPage.Leds);
        public static readonly SoonItem CarDataForAcAccLmu = new SoonItem(479, "Car data for AC, ACC and LMU", PanelPage.Leds);
        public static readonly SoonItem PitLimiterLights = new SoonItem(509, "Pit limiter lights", PanelPage.Leds);

        // Matrix.
        public static readonly SoonItem RpmColourForEverything = new SoonItem(371, "RPM colour for everything", PanelPage.Matrix);
        public static readonly SoonItem SimHubDevice = new SoonItem(363, "SimHub device", PanelPage.Matrix);
        public static readonly SoonItem PriorityOrder = new SoonItem(505, "Priority order", PanelPage.Matrix);

        // Rig.
        public static readonly SoonItem RealHardware = new SoonItem(506, PanelRigMap.RealHardwareTitle, PanelPage.Rig);

        // Shortcuts.
        public static readonly SoonItem RigTest = new SoonItem(511, "Rig test", PanelPage.Shortcuts);
        public static readonly SoonItem AlertDismissal = new SoonItem(510, "Alert dismissal", PanelPage.Shortcuts);

        // Settings.
        public static readonly SoonItem FuelTargetPerLap = new SoonItem(326, "Fuel target per lap", PanelPage.Settings);
        public static readonly SoonItem TyreDisplay = new SoonItem(325, "Tyre display", PanelPage.Settings);
        public static readonly SoonItem YellowFlags = new SoonItem(504, "Yellow flags", PanelPage.Settings);
        public static readonly SoonItem TyreWear = new SoonItem(507, "Tyre wear", PanelPage.Settings);
        public static readonly SoonItem PitWindowOpen = new SoonItem(507, "Pit window open", PanelPage.Settings);
        public static readonly SoonItem Incidents = new SoonItem(508, "Incidents", PanelPage.Settings);
        public static readonly SoonItem HybridBatteryLow = new SoonItem(737, "Hybrid battery low", PanelPage.Settings);
        public static readonly SoonItem AlertDisplay = new SoonItem(512, "Alert display", PanelPage.Settings);
        public static readonly SoonItem SimTimeOfDay = new SoonItem(740, "Sim time of day", PanelPage.Settings);
        public static readonly SoonItem ScreenDimming = new SoonItem(740, "Screen dimming", PanelPage.Settings);
        public static readonly SoonItem DashTheme = new SoonItem(735, "Theme", PanelPage.Settings);
        public static readonly SoonItem ColourVision = new SoonItem(129, "Colour vision", PanelPage.Settings);
        public static readonly SoonItem Colours = new SoonItem(127, "Colours", PanelPage.Settings);
        public static readonly SoonItem BrandName = new SoonItem(484, "Name", PanelPage.Settings);
        public static readonly SoonItem BrandRaceNumber = new SoonItem(484, "Race number", PanelPage.Settings);
        public static readonly SoonItem BrandLogo = new SoonItem(484, "Logo", PanelPage.Settings);
        public static readonly SoonItem IdleScreenBackground = new SoonItem(736, "Idle screen background", PanelPage.Settings);

        /// <summary>Every entry, in the order the pages draw them. After the entries, which static fields are
        /// initialised in.</summary>
        public static readonly IReadOnlyList<SoonItem> All = new[]
        {
            RevFill, SpotterAtRevBarEnds, PitPageInPitLane, PopUps, DeltaEdgeLights, ScreenCare, Fit, CircleTracker, Launch,
            FlagsScreen, YourDisplays, ZonesInsteadOfCards,
            EachLedInTurn, IdleSweep, EngineStartAnimation, CarDataForAcAccLmu, PitLimiterLights,
            RpmColourForEverything, SimHubDevice, PriorityOrder,
            RealHardware,
            RigTest, AlertDismissal,
            FuelTargetPerLap, TyreDisplay, YellowFlags, TyreWear, PitWindowOpen, Incidents, HybridBatteryLow, AlertDisplay,
            SimTimeOfDay, ScreenDimming, DashTheme, ColourVision, Colours, BrandName, BrandRaceNumber, BrandLogo, IdleScreenBackground,
        };

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
