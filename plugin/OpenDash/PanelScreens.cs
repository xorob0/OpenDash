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

        /// <summary>The line an upgrading user meets over the cards, and nobody else: the noun and the two
        /// verbs Home's issue uses for the same screens (PanelAttention.UnclaimedDetail).</summary>
        public const string UnclaimedNote = "Keep or remove each screen an older OpenDash made.";

        /// <summary>A card's state: its dashboard is in SimHub, or it is gone.</summary>
        public const string InSimHub = "In SimHub";
        public const string Missing = "Missing";

        /// <summary>The fix box under a screen whose dashboard is gone; its detail is PanelAttention.MissingDetail.</summary>
        public const string MissingTitle = "This screen's dashboard is missing from SimHub";

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

        /// <summary>A screen's header press that copies it.</summary>
        public const string DuplicateTooltip = "Adds a second screen set up like this one.";

        // The rows' titles, which the panes draw and search lists, so the two cannot drift apart.
        public const string FlagDisplayTitle = "Flag display";
        public const string LapReviewTitle = "Lap review";
        public const string ZonesTitle = "Zones";
        public const string PitWallPageTitle = "Page";
        public const string WebViewTitle = "Web view address";
        public const string ModulesTitle = "Modules";
        public const string FirstModuleTitle = "First module";
        public const string SlotsTitle = "Slots";

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
            new PanelSearch.Entry(FlagDisplayTitle, PanelPage.Screens, AnchorFlagDisplay, "band d", "full screen"),
            new PanelSearch.Entry(LapReviewTitle, PanelPage.Screens, AnchorLapReview, "last lap"),
            new PanelSearch.Entry(ZonesTitle, PanelPage.Screens, AnchorZones, "pages", "zone a", "zone b", "zone c", "band d", "class"),
            new PanelSearch.Entry(PitWallPageTitle, PanelPage.Screens, AnchorPitWallPage, "pit wall", "race", "tower", "telemetry"),
            new PanelSearch.Entry(WebViewTitle, PanelPage.Screens, AnchorWebView, "url", "pit wall"),
            new PanelSearch.Entry(ModulesTitle, PanelPage.Screens, AnchorModules, "companion", "phone", "rotation"),
            new PanelSearch.Entry(FirstModuleTitle, PanelPage.Screens, AnchorFirstModule, "companion", "phone", "start"),
            new PanelSearch.Entry(SlotsTitle, PanelPage.Screens, AnchorSlots, "card face", "round", "cards"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>Search labels this page draws through something other than the constant: none.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>();
    }
}
