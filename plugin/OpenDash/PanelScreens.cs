// PanelScreens.cs: the Screens page's words and decisions -- its title, what search finds on it, and the line
// it puts over its cards for a rig the migration made.
//
// Apart from the WPF file for the reason PanelCopy.cs is apart from Widgets.cs: the panel is net48 and the
// net8.0 test project cannot compile a line of it, so the decision and its wording live where
// PanelScreensTests can hold them against a rig built the way the plugin builds one. The Screens page agent
// owns this file and adds to it. Pure: no WPF types.
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelScreens
    {
        public const string Title = "Screens";

        /// <summary>A screen written after SimHub started, on its card and in the fix box under it.</summary>
        public const string RestartToLoad = "Restart SimHub to load it";

        /// <summary>A rig with no screen, on this page's pill and on Home's card.</summary>
        public const string NoScreens = "No screens yet";

        /// <summary>The line an upgrading user meets over the cards, and nobody else.</summary>
        public const string UnclaimedNote = "Remove the dashboards you have no screen for.";

        /// <summary>
        /// Whether the rig holds a screen the migration made that the driver has neither kept nor removed.
        /// </summary>
        /// <remarks>
        /// It used to be whether the rig held more than four screens, which says nothing about where a
        /// screen came from (#478). The screen carries that fact now (ScreenInstance.Unclaimed), so the line
        /// is shown however few screens the migration left and never to screens added from the Screens page,
        /// however many. It goes on its own once the last such screen is kept or removed, which is when it
        /// stops being true, and there is nothing to dismiss.
        /// </remarks>
        public static bool ShowsUnclaimedNote(IEnumerable<ScreenInstance> rig)
        {
            return rig != null && rig.Any(screen => screen != null && screen.Unclaimed == true);
        }

        // Anchors the page's rows carry, so search can scroll to them.
        public const string AnchorCards = "screens.cards";
        public const string AnchorRevBar = "screens.revbar";
        public const string AnchorFlagDisplay = "screens.flag-display";
        public const string AnchorLapReview = "screens.lap-review";
        public const string AnchorZones = "screens.zones";
        public const string AnchorPitWallPage = "screens.pitwall-page";
        public const string AnchorWebView = "screens.webview";
        public const string AnchorModules = "screens.modules";
        public const string AnchorFirstModule = "screens.first-module";
        public const string AnchorSlots = "screens.slots";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(Title, PanelPage.Screens, AnchorCards, "your screens", "rig", "dashboard", "display"),
            new PanelSearch.Entry(PanelAddScreen.AddButton, PanelPage.Screens, AnchorCards, "new", "dashboard", "display"),
            new PanelSearch.Entry(PanelDataTab.RevBarTitle, PanelPage.Screens, AnchorRevBar, "shift lights", "rev bar", "rpm"),
            new PanelSearch.Entry("Flag display", PanelPage.Screens, AnchorFlagDisplay, "band d", "full screen"),
            new PanelSearch.Entry("Lap review", PanelPage.Screens, AnchorLapReview, "last lap"),
            new PanelSearch.Entry("Zones", PanelPage.Screens, AnchorZones, "pages", "zone a", "zone b", "zone c", "band d", "class"),
            new PanelSearch.Entry("Page", PanelPage.Screens, AnchorPitWallPage, "pit wall", "race", "tower", "telemetry"),
            new PanelSearch.Entry("Web view address", PanelPage.Screens, AnchorWebView, "url", "pit wall"),
            new PanelSearch.Entry("Modules", PanelPage.Screens, AnchorModules, "companion", "phone", "rotation"),
            new PanelSearch.Entry("First module", PanelPage.Screens, AnchorFirstModule, "companion", "phone", "start"),
            new PanelSearch.Entry("Slots", PanelPage.Screens, AnchorSlots, "card face", "round", "cards"),
        };
    }
}
