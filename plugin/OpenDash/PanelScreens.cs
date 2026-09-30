// PanelScreens.cs: the Screens page's words and decisions -- its title, what search finds on it, what a card
// says about its screen, the zone list a face is configured through, and every row title the page draws.
//
// Apart from the WPF files for the reason PanelCopy.cs is apart from Widgets.cs: the panel is net48 and the
// net8.0 test project cannot compile a line of it, so each decision and its wording live where
// PanelScreensTests can hold them against a rig built the way the plugin builds one. The WPF files
// (SettingsControl.Screens*.cs) only draw what this file decides. Pure: no WPF types.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>What a screen's card says about it: SimHub has it, SimHub has not read it yet, or it is gone.</summary>
    public enum ScreenState
    {
        InSimHub,
        Restart,
        Missing,
    }

    /// <summary>One page of a face zone as the zone list draws it.</summary>
    public sealed class ZoneRow
    {
        public ZoneRow(int page, string name, bool ticked, bool first, bool notInIracing, bool locked)
        {
            Page = page;
            Name = name;
            Ticked = ticked;
            First = first;
            NotInIracing = notInIracing;
            Locked = locked;
        }

        /// <summary>The page's number in the zone's catalogue.</summary>
        public int Page { get; private set; }

        public string Name { get; private set; }

        /// <summary>In the zone's cycle.</summary>
        public bool Ticked { get; private set; }

        /// <summary>The first ticked page in the zone's order, which is the page it opens on.</summary>
        public bool First { get; private set; }

        /// <summary>A page iRacing publishes nothing for; still tickable, since other sims fill it.</summary>
        public bool NotInIracing { get; private set; }

        /// <summary>The last ticked page, which cannot be unticked: a zone with an empty cycle draws nothing.</summary>
        public bool Locked { get; private set; }
    }

    /// <summary>One end field of a face's info bar, as the Info bar aside lists it.</summary>
    public sealed class BarRow
    {
        public BarRow(string slot, string label)
        {
            Slot = slot;
            Label = label;
        }

        /// <summary>One of Contract.BarSlots.</summary>
        public string Slot { get; private set; }

        public string Label { get; private set; }
    }

    public static class PanelScreens
    {
        public const string Title = "Screens";

        /// <summary>A rig with no screen, on Home's card.</summary>
        public const string NoScreens = "No screens yet";

        /// <summary>The line an upgrading user meets over the cards, and nobody else: the noun and the two
        /// verbs Home's issue uses for the same screens (PanelAttention.UnclaimedDetail).</summary>
        public const string UnclaimedNote = "Keep or remove each screen an older OpenDash made.";

        // --- A card's state ----------------------------------------------------------------------------

        /// <summary>A card's state: its dashboard is in SimHub.</summary>
        public const string InSimHub = "In SimHub";

        /// <summary>A card's state, and the title of the fix box under it: written after SimHub started, so
        /// SimHub has not read it. One phrase for the one state, on the card and in the box.</summary>
        public const string RestartToLoad = "Restart SimHub to load it";

        /// <summary>A card's state: the dashboard's folder is gone.</summary>
        public const string Missing = "Missing";

        /// <summary>The fix box under a screen whose dashboard is gone; its detail is PanelAttention.MissingDetail.</summary>
        public const string MissingTitle = "This screen's dashboard is missing from SimHub";

        /// <summary>The step after the restart, under <see cref="RestartToLoad"/>: SimHub lists the dashboard
        /// under the screen's name, so that is the name to look for.</summary>
        public static string RestartDetail(string name)
        {
            return "Then assign \"" + name + "\" to this display in Dash Studio.";
        }

        /// <summary>The state a card shows. A missing folder wins, because restarting will not bring it back.</summary>
        public static ScreenState StateOf(bool installed, bool restart)
        {
            if (!installed) return ScreenState.Missing;
            return restart ? ScreenState.Restart : ScreenState.InSimHub;
        }

        public static string StateLabel(ScreenState state)
        {
            switch (state)
            {
                case ScreenState.Missing: return Missing;
                case ScreenState.Restart: return RestartToLoad;
                default: return InSimHub;
            }
        }

        /// <summary>The state's dot and ink: green in SimHub, amber waiting for a restart, red when gone.</summary>
        public static string StateHex(ScreenState state)
        {
            switch (state)
            {
                case ScreenState.Missing: return Theme.StatusFailed;
                case ScreenState.Restart: return Theme.Caution;
                default: return Theme.StatusUpToDate;
            }
        }

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

        /// <summary>The kind under a card's name, as Screens.dc.html writes it: the header carries the size.</summary>
        public static string CardMeta(ScreenInstance screen)
        {
            return screen == null ? string.Empty : PanelAddScreen.KindName(screen.Kind);
        }

        /// <summary>"Face · 1280 × 480" beside the selected screen's name; the kind alone when its package is
        /// gone and it has no size to show.</summary>
        public static string Facts(ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            var kind = PanelAddScreen.KindName(screen.Kind);
            return screen.Width > 0 && screen.Height > 0 ? kind + " · " + screen.SizeLabel : kind;
        }

        // --- The header's presses -----------------------------------------------------------------------

        public const string EditButton = "Edit";
        public const string EditTooltip = "Change this screen's name or size, or install its dashboard again.";
        public const string DuplicateButton = "Duplicate";

        /// <summary>A screen's header press that copies it.</summary>
        public const string DuplicateTooltip = "Adds a second screen set up like this one.";

        public const string RemoveButton = "Remove";
        public const string RemoveTooltip = "Removes this screen, its settings and its dashboard.";

        // --- The remove sheet ---------------------------------------------------------------------------

        public static string RemoveTitle(string name)
        {
            return "Remove " + name;
        }

        /// <summary>Whether a wheel button can be bound to this screen: every kind with actions of its own.</summary>
        public static bool HasActions(ScreenInstance screen)
        {
            return screen != null && (screen.IsFace || screen.IsPitWall || screen.IsCompanion);
        }

        /// <summary>What removing costs, and the bound buttons that stop working where the kind has actions.</summary>
        public static string RemoveBody(bool hasActions)
        {
            return "Removes the screen, its dashboard and its settings." + (hasActions ? " " + BoundButtonsStop : string.Empty);
        }

        public const string BoundButtonsStop = "Any wheel button you bound to it stops working.";
        public const string KeepButton = "Keep it";
        public const string KeepTooltip = "Leaves this screen alone.";
        public const string RemoveItButton = "Remove it";

        public static string Removed(string name)
        {
            return "Removed " + name + ". SimHub still lists its dashboard until you restart it.";
        }

        public static string RemoveFailed(string name, string error)
        {
            return "Removed " + name + ", but its dashboard could not be deleted: " + error;
        }

        // --- A face's rows ------------------------------------------------------------------------------

        /// <summary>The face's rev bar, which is the screen's own.</summary>
        public const string RevBarTitle = "Rev bar";

        public const string FlagDisplayTitle = "Flag display";

        /// <summary>A face's two flag formats, in Contract.FlagFormats order.</summary>
        public static readonly string[] FlagLabels = { "Band D", "Full screen" };

        /// <summary>A pit wall's and a companion's three, in Contract.CompanionFlagFormats order.</summary>
        public static readonly string[] BarFlagLabels = { "Off", "Bar", "Full screen" };

        public const string LapReviewTitle = "Lap review";

        /// <summary>In Contract.LapReviewModes order.</summary>
        public static readonly string[] LapReviewLabels = { "Off", "Races", "Always" };

        /// <summary>What the label cannot say: what is shown, and for how long.</summary>
        public const string LapReviewCaption = "Shows your last lap for four seconds after the line.";

        /// <summary>The body of a face whose package this build no longer carries.</summary>
        public static string NoLongerShipped(string sizeLabel)
        {
            return "OpenDash no longer ships a " + sizeLabel + " face. Your settings are kept.";
        }

        // --- The face's picture and its asides -----------------------------------------------------------

        /// <summary>The key the picture's bar is selected under, beside the zone letters.</summary>
        public const string BarKey = "BAR";

        /// <summary>The aside the bar opens, and the bar's search label.</summary>
        public const string InfoBarTitle = "Info bar";

        /// <summary>The bar's middle cell, which is fixed: the car's settings.</summary>
        public const string InfoBarMiddle = "Car settings";

        /// <summary>Under a zone that no wheel button advances.</summary>
        public const string NoButton = "No button";

        /// <summary>What the aside shows: the bar or zone last picked on this screen where the face has it, and
        /// the first zone of the picture otherwise.</summary>
        public static string AsideKey(string picked, Contract.FaceSize face)
        {
            if (picked == BarKey && face.HasBar) return BarKey;
            if (picked != null && Array.IndexOf(Contract.FaceZoneLetters, picked) >= 0) return picked;
            return PanelFacePlan.ZoneOrder(face)[0];
        }

        /// <summary>The ends of the bar in the order the aside lists them: two fields an end on a wide face,
        /// one on the portrait. A face without a bar has none.</summary>
        public static IReadOnlyList<BarRow> BarRows(Contract.FaceSize face)
        {
            if (!face.HasBar) return new BarRow[0];
            if (face.BarFieldsPerEnd == 1)
            {
                return new[] { new BarRow("Left1", "Left"), new BarRow("Right1", "Right") };
            }
            return new[]
            {
                new BarRow("Left1", "Left, first"),
                new BarRow("Left2", "Left, second"),
                new BarRow("Right1", "Right, first"),
                new BarRow("Right2", "Right, second"),
            };
        }

        /// <summary>What one end of the bar shows, as its cell in the picture writes it: "Race time · Lap".</summary>
        public static string BarEnd(FaceSettings settings, Contract.FaceSize face, bool left)
        {
            if (settings == null) return string.Empty;
            var first = FacePages.FieldName(settings.BarField(left ? "Left1" : "Right1"));
            if (face.BarFieldsPerEnd == 1) return first;
            return FacePages.EndLabel(first, FacePages.FieldName(settings.BarField(left ? "Left2" : "Right2")));
        }

        /// <summary>"4 of 21": the pages a zone cycles, of the pages it could.</summary>
        public static string ZoneCount(FaceSettings settings, string letter)
        {
            var pages = FacePages.For(letter);
            var on = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                if (settings != null && settings.PageEnabled(letter, pages[i].Number)) on++;
            }
            return on + " of " + pages.Count;
        }

        /// <summary>The first ticked page in the zone's order: the page it opens on (ruling 29).</summary>
        public static int FirstTicked(FaceSettings settings, string letter)
        {
            if (settings == null) return 0;
            foreach (var page in settings.Order(letter))
            {
                if (settings.PageEnabled(letter, page)) return page;
            }
            return settings.Start(letter);
        }

        /// <summary>The line under a zone of the picture: the button that advances it, "No button" when none
        /// does, and nothing when SimHub's bindings cannot be read, which says nothing either way.</summary>
        public static string ZoneButtonLine(IList<string> triggers)
        {
            if (triggers == null) return string.Empty;
            return PanelBindings.ChipText(triggers) ?? NoButton;
        }

        // --- The zone aside ----------------------------------------------------------------------------

        public const string ShowAll = "Show all";
        public const string OnlyTicked = "Only ticked";
        public const string AllPages = "All";
        public const string NoPages = "None";
        public const string DragHint = "Drag to reorder";
        public const string FirstTag = "First";
        public const string NotInIracing = "Not in iRacing";
        public const string ClassOnlyTitle = "My class only";
        public const string NextPageTitle = "Next page";

        /// <summary>The hover of the one page left ticked, which stays ticked.</summary>
        public const string LastPageTooltip = "A zone keeps at least one page.";
        public const string PreviousPageTitle = "Previous page";

        /// <summary>The pages iRacing publishes nothing for, by id: the zone list and the module grid say so
        /// beside them and leave them tickable.</summary>
        private static readonly string[] NoIracingData = { "energy", "damage", "trackRivals" };

        public static bool IsNotInIracing(string pageId)
        {
            return pageId != null && Array.IndexOf(NoIracingData, pageId) >= 0;
        }

        /// <summary>
        /// A zone's pages in the order the list draws them: the ticked ones in the zone's order, then, with
        /// Show all, the others in the same order.
        /// </summary>
        public static IReadOnlyList<ZoneRow> ZoneRows(FaceSettings settings, string letter, bool showAll)
        {
            var pages = FacePages.For(letter);
            var order = settings == null ? Contract.NormaliseOrder(null, pages.Count) : settings.Order(letter);
            var ticked = order.Where(page => settings != null && settings.PageEnabled(letter, page)).ToList();
            var rows = new List<ZoneRow>();
            for (var i = 0; i < ticked.Count; i++)
            {
                var page = ticked[i];
                rows.Add(new ZoneRow(page, FacePages.NameOf(letter, page), true, i == 0, IsNotInIracing(FacePages.IdOf(letter, page)), ticked.Count == 1));
            }
            if (!showAll) return rows;
            foreach (var page in order)
            {
                if (ticked.Contains(page)) continue;
                rows.Add(new ZoneRow(page, FacePages.NameOf(letter, page), false, false, IsNotInIracing(FacePages.IdOf(letter, page)), false));
            }
            return rows;
        }

        /// <summary>
        /// The zone's whole order after a row of the list was dragged from one place to another: the rows as
        /// drawn, moved, and every page the list did not draw after them in the order it had.
        /// </summary>
        public static int[] Reordered(FaceSettings settings, string letter, bool showAll, int from, int to)
        {
            var drawn = ZoneRows(settings, letter, showAll).Select(row => row.Page).ToList();
            var moved = PanelReorder.Move(drawn, from, to).ToList();
            var order = settings == null ? Contract.NormaliseOrder(null, FacePages.For(letter).Count) : settings.Order(letter);
            foreach (var page in order)
            {
                if (!moved.Contains(page)) moved.Add(page);
            }
            return moved.ToArray();
        }

        /// <summary>Whether a zone lists, under Show all, the two modules B and C are waiting for (Circle
        /// tracker and Launch): the zones that draw from the module catalogue.</summary>
        public static bool ListsSoonModules(string letter)
        {
            return letter == "B" || letter == "C";
        }

        // --- The quick glance ---------------------------------------------------------------------------

        /// <summary>The zones a face's glance can borrow, in Contract.FaceZoneLetters order.</summary>
        public static string[] GlanceZoneLabels()
        {
            return Contract.FaceZoneLetters.Select(PanelFacePlan.ZoneLabel).ToArray();
        }

        /// <summary>The pages of one of those zones, and nothing it does not carry.</summary>
        public static string[] GlancePageLabels(int zoneIndex)
        {
            return FacePages.For(Contract.FaceZoneLetters[zoneIndex]).Select(page => page.Name).ToArray();
        }

        /// <summary>
        /// The glance after its zone was changed: the same page where the new zone carries it -- zone A's
        /// Track and module 13 are one drawing -- and the new zone's first page otherwise.
        /// </summary>
        public static int GlanceWithZone(int glance, int zoneIndex)
        {
            var current = Contract.NormaliseQuickGlance(glance);
            var from = Contract.FaceZoneLetters[Contract.QuickGlanceZone(current)];
            var id = FacePages.IdOf(from, Contract.QuickGlancePage(current));
            var pages = FacePages.For(Contract.FaceZoneLetters[zoneIndex]);
            var page = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                if (string.Equals(pages[i].Id, id, StringComparison.Ordinal)) page = pages[i].Number;
            }
            return Contract.QuickGlanceValue(zoneIndex, page);
        }

        /// <summary>A pit wall's glance zones, named as its pages name them: "Tower B".</summary>
        public static string[] PitWallGlanceZoneLabels()
        {
            return Contract.GlanceZoneSlots().Select(slot => slot.Page + " " + slot.Slot).ToArray();
        }

        // --- Details -------------------------------------------------------------------------------------

        public const string DetailsTitle = "Details";
        public const string SimHubNameLabel = "SimHub name";
        public const string FolderLabel = "Folder";
        public const string PropertiesLabel = "Properties";
        public const string VersionLabel = "Version";
        public const string NotInstalled = "Not installed";

        /// <summary>
        /// The properties the screen publishes, as a reader would type them: the namespace is frozen at
        /// creation and is not the screen's name (ADR 0017), there is no dot between it and a setting, the
        /// stock pit wall's address predates the idiom, and a round screen reads the shared slots.
        /// </summary>
        public static string Properties(ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            if (screen.IsSlots)
            {
                return Contract.Prefix + "." + Contract.SlotProperty(1) + " to " + Contract.Prefix + "." + Contract.SlotProperty(CardsRead(screen));
            }
            var own = Contract.Prefix + "." + screen.Namespace + "*";
            if (screen.IsPitWall && string.Equals(screen.Namespace, Contract.PitWallPrefix, StringComparison.Ordinal))
            {
                return own + " and " + Contract.Prefix + "." + Contract.WebViewUrl;
            }
            return own;
        }

        // --- A pit wall ----------------------------------------------------------------------------------

        /// <summary>Which of the three landscape pages the wall shows.</summary>
        public const string PitWallPageTitle = "Page on screen";

        public const string WebViewTitle = "Web view address";

        /// <summary>The box's hover while it is empty; its whole address once it has one.</summary>
        public const string WebViewEmptyTooltip = "http or https only.";

        public const string PortraitTitle = "Portrait layout";

        /// <summary>"Race zones": the eyebrow over the zone list beside the picture.</summary>
        public static string PitWallZonesLabel(string page)
        {
            return page + " zones";
        }

        /// <summary>"Zone A", or "Wide zone" for the one that spans a column.</summary>
        public static string PitWallZoneLabel(Contract.PitWallZoneSlot slot)
        {
            if (slot == null) return string.Empty;
            return slot.Wide ? "Wide zone" : "Zone " + slot.Slot;
        }

        /// <summary>The landscape zones of one page, in the order the list draws them.</summary>
        public static IReadOnlyList<Contract.PitWallZoneSlot> PitWallZones(int page)
        {
            var name = Contract.PitWallPageNames[Contract.NormalisePitWallPage(page)];
            return Contract.PitWallZoneSlots.Where(slot => slot.Landscape && string.Equals(slot.Page, name, StringComparison.Ordinal)).ToList();
        }

        /// <summary>The portrait package's four zones.</summary>
        public static IReadOnlyList<Contract.PitWallZoneSlot> PortraitZones()
        {
            return Contract.PitWallZoneSlots.Where(slot => !slot.Landscape).ToList();
        }

        /// <summary>"A · Fuel": one portrait zone's choices, each naming the zone it sets.</summary>
        public static string[] PortraitLabels(string letter)
        {
            return ZonePages.Standard.Select(page => letter + " · " + page.Name).ToArray();
        }

        /// <summary>A pit wall standing on end, which draws the portrait layout and not the three pages.</summary>
        public static bool IsPortrait(ScreenInstance screen)
        {
            return screen != null && screen.Height > screen.Width;
        }

        // --- A companion ---------------------------------------------------------------------------------

        public const string ModulesTitle = "Modules";
        public const string FirstModuleTitle = "First module";
        public const string NextModuleTitle = "Next module";

        /// <summary>"18 of 21": the modules in the rotation.</summary>
        public static string ModuleCount(bool[] modules)
        {
            var on = modules == null ? 0 : modules.Take(Modules.Count).Count(m => m);
            return on + " of " + Modules.Count;
        }

        public static string[] ModuleNames()
        {
            return Modules.All.Select(module => module.Name).ToArray();
        }

        /// <summary>Where a wheel button that pages the companion is bound, after the device is opened.</summary>
        public static readonly string[] CompanionPagingCrumbs = { "Controls and events", "NextScreen" };

        // --- A round screen ------------------------------------------------------------------------------

        /// <summary>The round screen's rev ring, which is the rig-wide rev bar.</summary>
        public const string RevRingTitle = "Rev ring";

        /// <summary>The round pane's search label, drawn as its Card rows.</summary>
        public const string CardsTitle = "Cards";

        public const string CardsCaption = "Cards are shared by every round screen.";

        public static string CardLabel(int slot)
        {
            return "Card " + slot;
        }

        /// <summary>
        /// How many slots the screen's package reads: the 480 round two, the 800 round six, and a card face
        /// of any other size all twelve.
        /// </summary>
        public static int CardsRead(ScreenInstance screen)
        {
            if (screen == null) return Contract.SlotCount;
            if (screen.Width == 480 && screen.Height == 480) return 2;
            if (screen.Width == 800 && screen.Height == 800) return 6;
            return Contract.SlotCount;
        }

        /// <summary>
        /// The caption under the round pane's Rev ring, which writes the rig-wide OpenDash.RevBar. It reaches
        /// further than the round screens: the companion's speedo reads it, and so does every face whose own
        /// Revbar was never set, since ScreenRevBar falls back to the rig's while a screen's is null and a new
        /// face starts null. "Applies to every round face on your rig." let a driver turn off the main face's
        /// rev bar from the round pane unawares; ruling 38's "Every round screen and the phone's speedo." left
        /// those faces out too.
        /// </summary>
        public const string RigRevBarCaption = "Every round screen, the phone's speedo, and any screen whose own Revbar you have not set.";

        // --- Anchors, search and the greyed rows ---------------------------------------------------------

        public const string AnchorCards = "screens.cards";
        public const string AnchorRevBar = "screens.revbar";
        public const string AnchorFlagDisplay = "screens.flag-display";
        public const string AnchorLapReview = "screens.lap-review";
        public const string AnchorZones = "screens.zones";
        public const string AnchorGlance = "screens.glance";
        public const string AnchorClassOnly = "screens.class-only";
        public const string AnchorDetails = "screens.details";
        public const string AnchorPitWallPage = "screens.pitwall-page";
        public const string AnchorWebView = "screens.webview";
        public const string AnchorPortrait = "screens.portrait";
        public const string AnchorModules = "screens.modules";
        public const string AnchorFirstModule = "screens.first-module";
        public const string AnchorPaging = "screens.paging";
        public const string AnchorSlots = "screens.slots";
        public const string AnchorRevRing = "screens.rev-ring";

        /// <summary>The zones of a face, found by what they are called; the page draws them as their letters.</summary>
        public const string ZonesTitle = "Zones";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(Title, PanelPage.Screens, AnchorCards, "your screens", "rig", "dashboard", "display"),
            new PanelSearch.Entry(PanelAddScreen.AddButton, PanelPage.Screens, AnchorCards, "new", "dashboard", "display"),
            new PanelSearch.Entry(RevBarTitle, PanelPage.Screens, AnchorRevBar, "shift lights", "revbar", "rpm"),
            new PanelSearch.Entry(FlagDisplayTitle, PanelPage.Screens, AnchorFlagDisplay, "band d", "full screen", "flags"),
            new PanelSearch.Entry(LapReviewTitle, PanelPage.Screens, AnchorLapReview, "last lap"),
            new PanelSearch.Entry(ZonesTitle, PanelPage.Screens, AnchorZones, "pages", "zone a", "zone b", "zone c", "band d", "reorder"),
            new PanelSearch.Entry(InfoBarTitle, PanelPage.Screens, AnchorZones, "bar", "race time", "position"),
            new PanelSearch.Entry(NextPageTitle, PanelPage.Screens, AnchorZones, "zone", "button"),
            new PanelSearch.Entry(PreviousPageTitle, PanelPage.Screens, AnchorZones, "zone", "back", "button"),
            new PanelSearch.Entry(ClassOnlyTitle, PanelPage.Screens, AnchorClassOnly, "class"),
            new PanelSearch.Entry(PanelShortcuts.QuickGlanceTitle, PanelPage.Screens, AnchorGlance, "hold", "glance"),
            new PanelSearch.Entry(DetailsTitle, PanelPage.Screens, AnchorDetails, "folder", "properties", "version", "namespace"),
            new PanelSearch.Entry(PitWallPageTitle, PanelPage.Screens, AnchorPitWallPage, "pit wall", "race", "tower", "telemetry"),
            new PanelSearch.Entry(WebViewTitle, PanelPage.Screens, AnchorWebView, "url", "pit wall"),
            new PanelSearch.Entry(PortraitTitle, PanelPage.Screens, AnchorPortrait, "pit wall", "portrait"),
            new PanelSearch.Entry(ModulesTitle, PanelPage.Screens, AnchorModules, "companion", "phone", "rotation"),
            new PanelSearch.Entry(FirstModuleTitle, PanelPage.Screens, AnchorFirstModule, "companion", "phone", "start"),
            new PanelSearch.Entry(NextModuleTitle, PanelPage.Screens, AnchorPaging, "companion", "phone", "paging", "nextscreen"),
            new PanelSearch.Entry(CardsTitle, PanelPage.Screens, AnchorSlots, "card face", "round", "slots"),
            new PanelSearch.Entry(RevRingTitle, PanelPage.Screens, AnchorRevRing, "round", "revbar", "shift lights"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn =
        {
            PanelSoon.RevFill,
            PanelSoon.SpotterAtRevBarEnds,
            PanelSoon.PitPageInPitLane,
            PanelSoon.PopUps,
            PanelSoon.DeltaEdgeLights,
            PanelSoon.ScreenCare,
            PanelSoon.Fit,
            PanelSoon.CircleTracker,
            PanelSoon.Launch,
            PanelSoon.FlagsScreen,
            PanelSoon.YourDisplays,
            PanelSoon.ZonesInsteadOfCards,
        };

        /// <summary>Search labels this page draws through something other than the constant: the zones as
        /// their letters, and the round screen's cards as one row per slot.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>
        {
            { ZonesTitle, "PanelFacePlan.ZoneLabel(" },
            { CardsTitle, "PanelScreens.CardLabel(" },
        };
    }
}
