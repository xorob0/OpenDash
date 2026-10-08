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

        /// <summary>The page the zone opens on (its Start), which the list draws first.</summary>
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

        /// <summary>A rig with no screen: the line over the add tile on an empty Screens page, as LEDs and Matrix
        /// draw theirs (PanelLeds.NoStrips, PanelMatrix.NoPanels), and the words Home's card and the Updates table
        /// read from it.</summary>
        public const string NoScreens = "No screens yet.";

        /// <summary>The line an upgrading user meets over the cards, and nobody else: the noun and the two
        /// verbs Home's issue uses for the same screens (PanelAttention.UnclaimedDetail).</summary>
        public const string UnclaimedNote = "Keep or remove each screen an older OpenDash made.";

        // --- A card's state ----------------------------------------------------------------------------

        /// <summary>A card's state: its dashboard is in SimHub.</summary>
        public const string InSimHub = "In SimHub";

        /// <summary>A card's state, and the title of the fix box under it: the dashboard's folder is gone. One
        /// word for the one state, on the card and in the box, as PanelCopy.RestartToLoad is one phrase;
        /// Home's screen line draws this constant too, so the three say it alike.</summary>
        public const string Missing = "Missing";

        /// <summary>The fix box under a screen whose dashboard is gone, titled as its card is; its detail is
        /// <see cref="MissingDetailFor"/>.</summary>
        public const string MissingTitle = Missing;

        /// <summary>The hover of the fix box's PanelAttention.InstallAgain, in the button's verb.</summary>
        public const string InstallAgainTooltip = "Installs this screen's dashboard again.";

        /// <summary>The detail of the fix box under a missing dashboard, which names whose settings are kept:
        /// the box's one-word title gives "Its" nothing to point at. Home's issue keeps
        /// PanelAttention.MissingDetail, whose title already names the screen.</summary>
        public const string MissingDetail = "This screen's settings are kept.";

        /// <summary>The fix box's detail under a missing dashboard: <see cref="MissingDetail"/> where the screen
        /// has settings to keep, and nothing for a round screen, which has none of its own.</summary>
        public static string MissingDetailFor(ScreenInstance screen)
        {
            return OwnsSettings(screen) ? MissingDetail : null;
        }

        /// <summary>The step after the restart, under PanelCopy.RestartToLoad: SimHub lists the dashboard
        /// under the screen's name, so that is the name to look for. The panel's one phrasing of the step, on the
        /// Screens fix box, Home's issue and the Updates row alike: "its display", since Home's list and the
        /// Updates table have no display for "this" to point at.</summary>
        public static string RestartDetail(string name)
        {
            return "Then assign \"" + name + "\" to its display in Dash Studio.";
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
                case ScreenState.Restart: return PanelCopy.RestartToLoad;
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
        /// Whether a change saved in a screen's editor rebuilds the page as well as the editor: only the first
        /// change to a screen the migration made, since keeping it can end the line over the cards, the
        /// sidebar's warning and Home's issue, none of which the editor reaches. Asked before the save, which
        /// keeps the screen.
        /// </summary>
        public static bool RebuildsPageAfterSave(ScreenInstance screen)
        {
            return screen != null && screen.Unclaimed == true;
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
            return screen == null ? string.Empty : KindOf(screen);
        }

        /// <summary>"Face · 1280 × 480" beside the selected screen's name; the kind alone when its package is
        /// gone and it has no size to show.</summary>
        public static string Facts(ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            var kind = KindOf(screen);
            return screen.Width > 0 && screen.Height > 0 ? kind + " · " + screen.SizeLabel : kind;
        }

        /// <summary>A card face that is not round: the rectangular "OpenDash slots WxH" faces a migration can
        /// bring onto a rig, which read the shared cards as a round screen does.</summary>
        public const string CardFace = "Card face";

        /// <summary>Whether the screen is a round one: a card face as tall as it is wide.</summary>
        public static bool IsRound(ScreenInstance screen)
        {
            return screen != null && screen.IsSlots && screen.Width > 0 && screen.Width == screen.Height;
        }

        /// <summary>
        /// The screen's kind as the card and the header name it: PanelAddScreen.KindName, except a card face
        /// that is not round, which is not called "Round".
        /// </summary>
        public static string KindOf(ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            if (screen.IsSlots && !IsRound(screen)) return CardFace;
            return PanelAddScreen.KindName(screen.Kind);
        }

        /// <summary>The kind a card's picture is drawn as (Ui.Thumb): a ring for a round screen only, and a
        /// rectangular card face at its own proportions, as its editor draws no disc.</summary>
        public static string ThumbKind(ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            return IsRound(screen) ? "round" : screen.Kind;
        }

        /// <summary>The most cards the screens' grid lays in a row: Screens.dc.html's repeat(6, minmax(0, 1fr)),
        /// 10 apart (PanelKit.CardGridGap). Fewer where a card would fall under PanelKit.CardMinWidth.</summary>
        public const int CardColumns = 6;

        // --- The selected screen's block ------------------------------------------------------------------

        /// <summary>The block under the cards: 20 below its rule, its parts 18 apart (Screens.dc.html's
        /// padding-top 20 and gap 18).</summary>
        public const double SelectedTop = 20;

        public const double SelectedGap = 18;

        /// <summary>Between the screen's name and the facts after it in the header: the artboard's 12.</summary>
        public const double HeaderGap = 12;

        /// <summary>Details' label column: the artboard's grid-template-columns 140px 1fr.</summary>
        public const double DetailsLabelWidth = 140;

        /// <summary>One page of a zone list (.pg): 32 high, as the greyed pages under Show all are.</summary>
        public const double PageRowHeight = 32;

        // --- The header's presses -----------------------------------------------------------------------

        public const string EditButton = "Edit";
        /// <summary>Edit's hover: the edit sheet asks a size, or an orientation for a screen shipped both ways
        /// up, beside the name.</summary>
        public const string EditTooltip = "Changes this screen's name, size or orientation, or reinstalls its dashboard.";
        public const string DuplicateButton = "Duplicate";

        /// <summary>A screen's header press that copies it. It counts nothing: duplicating Rim beside Rim (2) adds
        /// a third.</summary>
        public const string DuplicateTooltip = "Adds another screen set up like this one.";

        public const string RemoveButton = "Remove";
        public const string RemoveTooltip = "Removes this screen, its dashboard and its settings.";

        /// <summary>Remove's hover for this screen: a round screen owns no settings, since its cards are the
        /// rig's shared slots and stay, so it says only what goes.</summary>
        public static string RemoveTooltipFor(ScreenInstance screen)
        {
            return OwnsSettings(screen) ? RemoveTooltip : "Removes this screen and its dashboard.";
        }

        /// <summary>Whether the screen has settings of its own that go with it: every kind but a round
        /// screen, whose cards are the twelve slots every round screen shares.</summary>
        public static bool OwnsSettings(ScreenInstance screen)
        {
            return screen == null || !screen.IsSlots;
        }

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

        /// <summary>What removing costs -- the settings only where the screen owns some -- and the bound
        /// buttons that stop working where the kind has actions.</summary>
        public static string RemoveBody(ScreenInstance screen)
        {
            var goes = OwnsSettings(screen) ? "Removes the screen, its dashboard and its settings." : "Removes the screen and its dashboard.";
            return goes
                + (HasActions(screen) ? " " + BoundButtonsStop : string.Empty)
                + (screen != null && screen.Theme != null ? " " + ThemedStopsSwitching : string.Empty);
        }

        public const string BoundButtonsStop = "Any wheel button you bound to it stops working.";

        /// <summary>What else goes with a themed screen, and what stays: the plugin takes its own playlist entries out
        /// with the screen (#199), and only those.</summary>
        public const string ThemedStopsSwitching = "Your displays stop switching to it by car; playlists you made in SimHub are kept.";
        public const string KeepButton = "Keep it";
        /// <summary>Keep keeps the screen (Save(screen) calls Keep), which answers the line over the cards'
        /// "Keep or remove", so the hover says keep as the button does.</summary>
        public const string KeepTooltip = "Keeps this screen.";
        public const string RemoveItButton = "Remove it";

        /// <summary>After a remove: SimHub reads its list at startup, so the step left is named, and the list
        /// named by where the driver meets it.</summary>
        public static string Removed(string name)
        {
            return "Removed " + name + ". Restart SimHub to take its dashboard out of Dash Studio.";
        }

        /// <summary>A remove whose folder stayed: "removed", as the sheet says, and the reason in the log, as
        /// PanelAddScreen.DuplicateFailed points there.</summary>
        public static string RemoveFailed(string name)
        {
            return "Removed " + name + ", but its dashboard could not be removed. See SimHub's log.";
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

        /// <summary>How long the dash shows the last lap: design/tokens.json's indicator.lapReview.durationMs,
        /// which PanelScreensTests holds this to, in seconds.</summary>
        public const int LapReviewSeconds = 4;

        /// <summary>What the label cannot say: what is shown, and for how long, in the words for
        /// <see cref="LapReviewSeconds"/>.</summary>
        public const string LapReviewCaption = "Shows your last lap for four seconds after the line.";

        /// <summary>The body of a face whose package this build no longer carries.</summary>
        public static string NoLongerShipped(string sizeLabel)
        {
            return "OpenDash no longer ships a " + sizeLabel + " face. Your settings are kept.";
        }

        /// <summary>
        /// The body of a themed face whose package this build no longer carries: its folder is left as it is, rather
        /// than written from the default package of its size (ADR 0016), and its displays stop switching to it.
        /// </summary>
        public static string ThemeNoLongerShipped(string theme, string sizeLabel)
        {
            var name = Contract.Themes.FirstOrDefault(t => string.Equals(t.Id, theme, StringComparison.Ordinal));
            return "OpenDash no longer ships the " + (name == null ? theme : name.Name) + " face at " + sizeLabel + ". Its dashboard and your settings are kept.";
        }

        // --- The face's picture and its asides -----------------------------------------------------------

        /// <summary>The key the picture's bar is selected under, beside the zone letters.</summary>
        public const string BarKey = "BAR";

        /// <summary>The aside the bar opens, and the bar's search label.</summary>
        public const string InfoBarTitle = "Info bar";

        /// <summary>The bar's middle cell, which is fixed: the car's settings.</summary>
        public const string InfoBarMiddle = "Car settings";

        /// <summary>Under a zone that no wheel button advances: the Next page chip's own words beside it, one
        /// phrase for the one state rather than the artboard's "No button".</summary>
        public const string NoButton = PanelBindings.NotBound;

        /// <summary>Whether a zone list shows every page after a part of the picture is picked: no, a zone picked
        /// opens on its ticked pages, as the artboard's does, whatever the last one showed.</summary>
        public const bool ShowAllAfterPick = false;

        /// <summary>What a screen reader hears of a part of the picture: whether it is the one the aside shows.</summary>
        public const string ZonePressed = "pressed";

        public const string ZoneNotPressed = "not pressed";

        public static string ZoneStatus(bool selected)
        {
            return selected ? ZonePressed : ZoneNotPressed;
        }

        /// <summary>The zone the aside opens on before anything is picked: C, as the artboard's does, on
        /// every face, whichever zone the body draws first.</summary>
        public const string FirstAside = "C";

        /// <summary>What the aside shows: the bar or zone last picked on this screen where the face has it, and
        /// zone C otherwise.</summary>
        public static string AsideKey(string picked, Contract.FaceSize face)
        {
            if (picked == BarKey && face.HasBar) return BarKey;
            if (picked != null && Array.IndexOf(Contract.FaceZoneLetters, picked) >= 0) return picked;
            return FirstAside;
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
            var pages = FacePages.For(letter, settings?.ThemeId());
            var on = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                if (settings != null && settings.PageEnabled(letter, pages[i].Number)) on++;
            }
            return on + " of " + pages.Count;
        }

        /// <summary>
        /// The page a zone opens on, which the list draws first and tags First: the zone's
        /// Start, the setting the dash reads, and never simply the first ticked page of its stored order.
        /// </summary>
        /// <remarks>
        /// A default face's zone C opens on Relative, page 14, while its stored order begins at Lap times; a
        /// list that took the order's head drew Lap times as First, and its first tick then wrote Lap times
        /// into the start. A start the mask has turned off, which only a file nothing has normalised can
        /// hold, reads forward in the zone's order as Normalise would move it.
        /// </remarks>
        public static int FirstTicked(FaceSettings settings, string letter)
        {
            var face = settings ?? new FaceSettings();
            var start = face.Start(letter);
            if (face.PageEnabled(letter, start)) return start;
            return Contract.FirstEnabledInOrder(start, face.Mask(letter), face.Order(letter));
        }

        /// <summary>The name of the page a face's zone or band opens on, which its cell in the picture draws:
        /// <see cref="FirstTicked"/>'s page, never the first of its order, which is Lap times in zone C where the
        /// zone opens on Relative.</summary>
        public static string OpensOnName(FaceSettings settings, string letter)
        {
            return FacePages.NameOf(letter, FirstTicked(settings, letter), settings?.ThemeId());
        }

        /// <summary>Whether a face's picture draws its rev strip lit: its own rev bar, as ScreenRevBar reads it,
        /// is anything but off.</summary>
        public static bool RevStripOn(string revBar)
        {
            return !string.Equals(revBar, Contract.RevBarOff, StringComparison.Ordinal);
        }

        /// <summary>
        /// The zone's order read from the page it opens on: the cycle wraps, so this is the same cycle its
        /// button steps through, begun where a session begins.
        /// </summary>
        public static int[] CycleFromStart(FaceSettings settings, string letter)
        {
            var order = settings == null ? Contract.NormaliseOrder(null, FacePages.For(letter).Count) : settings.Order(letter);
            return Rotated(order, FirstTicked(settings, letter));
        }

        /// <summary>An order turned so that <paramref name="head"/> leads it; the cycle it describes is the same.</summary>
        private static int[] Rotated(int[] order, int head)
        {
            var at = Array.IndexOf(order, head);
            if (at <= 0) return order;
            return order.Skip(at).Concat(order.Take(at)).ToArray();
        }

        /// <summary>The line under a zone of the picture: the button that advances it, "Not bound" when none
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
        public const string AllPagesTooltip = "Ticks every page.";
        public const string NoPages = "None";
        public const string NoPagesTooltip = "Unticks every page but the first.";
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
        /// A zone's pages in the order the list draws them: the ticked ones in the zone's cycle from the page
        /// it opens on, which is therefore the First, then, with Show all, the others in the same order.
        /// </summary>
        public static IReadOnlyList<ZoneRow> ZoneRows(FaceSettings settings, string letter, bool showAll)
        {
            var order = CycleFromStart(settings, letter);
            var ticked = order.Where(page => settings != null && settings.PageEnabled(letter, page)).ToList();
            var theme = settings?.ThemeId();
            var rows = new List<ZoneRow>();
            for (var i = 0; i < ticked.Count; i++)
            {
                var page = ticked[i];
                rows.Add(new ZoneRow(page, FacePages.NameOf(letter, page, theme), true, i == 0, IsNotInIracing(FacePages.IdOf(letter, page, theme)), ticked.Count == 1));
            }
            if (!showAll) return rows;
            foreach (var page in order)
            {
                if (ticked.Contains(page)) continue;
                rows.Add(new ZoneRow(page, FacePages.NameOf(letter, page, theme), false, false, IsNotInIracing(FacePages.IdOf(letter, page, theme)), false));
            }
            return rows;
        }

        /// <summary>
        /// The zone's whole order after a row of the list was dragged from one place to another: the rows as
        /// drawn, moved, then every page the list did not draw in the cycle's order, turned so that its head
        /// is the first ticked page -- the page the zone will open on.
        /// </summary>
        public static int[] Reordered(FaceSettings settings, string letter, bool showAll, int from, int to)
        {
            var drawn = ZoneRows(settings, letter, showAll).Select(row => row.Page).ToList();
            var moved = PanelReorder.Move(drawn, from, to).ToList();
            foreach (var page in CycleFromStart(settings, letter))
            {
                if (!moved.Contains(page)) moved.Add(page);
            }
            var order = moved.ToArray();
            var first = order.FirstOrDefault(page => settings != null && settings.PageEnabled(letter, page));
            return Rotated(order, first);
        }

        /// <summary>
        /// Applies a drag to the zone: its new order, and its start where the drag put another page first.
        /// </summary>
        /// <remarks>
        /// The start is written only when it moves, because FaceSettings.SetStart puts the running zone on it
        /// too: a drag that leaves the first page where it was must not snap the dash in front of the driver
        /// back to it.
        /// </remarks>
        public static void Reorder(FaceSettings settings, string letter, bool showAll, int from, int to)
        {
            if (settings == null) return;
            var order = Reordered(settings, letter, showAll, from, to);
            settings.SetOrder(letter, order);
            var first = order.FirstOrDefault(page => settings.PageEnabled(letter, page));
            if (first != settings.Start(letter)) settings.SetStart(letter, first);
        }

        /// <summary>
        /// A tick or an untick in the zone list. A page ticked joins the cycle after the last page already in
        /// it, where the list draws it and the zone's button reaches it last, as Screens.dc.html appends it;
        /// an untick only takes it out.
        /// </summary>
        /// <remarks>
        /// Ticking the mask alone put the page back at its old place in the stored order, so a page ticked at
        /// the foot of Show all jumped up the list and into the middle of the cycle. The page is moved in the
        /// stored order itself, next to the last ticked page, which is the same place in the cycle without
        /// turning the order: the position the dash publishes counts from the order's head. The start is not
        /// written, so the running zone stays where it is.
        /// </remarks>
        public static void Tick(FaceSettings settings, string letter, int page, bool on)
        {
            if (settings == null) return;
            if (!on || settings.PageEnabled(letter, page))
            {
                settings.SetPageEnabled(letter, page, on);
                return;
            }
            var last = CycleFromStart(settings, letter).LastOrDefault(p => p != page && settings.PageEnabled(letter, p));
            var order = settings.Order(letter).Where(p => p != page).ToList();
            order.Insert(order.IndexOf(last) + 1, page);
            settings.SetOrder(letter, order.ToArray());
            settings.SetPageEnabled(letter, page, true);
        }

        /// <summary>
        /// All ticks every page; None unticks every page but the one the zone opens on, because a zone with an
        /// empty cycle has nothing to draw. Neither moves the start.
        /// </summary>
        public static void SetEveryPage(FaceSettings settings, string letter, bool enabled)
        {
            if (settings == null) return;
            var pages = settings.Pages(letter);
            var keep = FirstTicked(settings, letter);
            for (var i = 0; i < pages.Count; i++)
            {
                if (enabled || pages[i].Number != keep) settings.SetPageEnabled(letter, pages[i].Number, enabled);
            }
        }

        /// <summary>Whether a zone lists, under Show all, the two modules B and C are waiting for (Circle
        /// tracker and Launch): the zones that draw from the module catalogue.</summary>
        public static bool ListsSoonModules(string letter)
        {
            return letter == "B" || letter == "C";
        }

        /// <summary>
        /// The line under the zone aside when two zones, or a zone and the glance, open on the same page:
        /// "Zone C and band D both show the relative.", one line per page, nothing when there is none.
        /// </summary>
        /// <remarks>
        /// FacePageClash.Warning says "zone D"; this page, its picker and its aside say "Band D", so the line
        /// names each zone as PanelFacePlan.ZoneLabel does.
        /// </remarks>
        public static string PageClash(FaceSettings settings)
        {
            var lines = FacePageClash.Find(settings).Select(clash =>
            {
                var parts = clash.Zones.Select(letter => Lower(PanelFacePlan.ZoneLabel(letter))).ToList();
                if (clash.Glance) parts.Add("the quick glance");
                var list = parts.Count == 2
                    ? parts[0] + " and " + parts[1]
                    : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
                var verb = parts.Count == 2 ? " both show " : " all show ";
                return char.ToUpperInvariant(list[0]) + list.Substring(1) + verb + FacePageClash.DisplayName(clash.PageName) + ".";
            });
            return string.Join(Environment.NewLine, lines);
        }

        private static string Lower(string words)
        {
            return string.IsNullOrEmpty(words) ? words : char.ToLowerInvariant(words[0]) + words.Substring(1);
        }

        /// <summary>The least a row's title and caption keep beside a line of controls.</summary>
        public const double RowTitleLeast = 140;

        /// <summary>
        /// The widest a line of controls may be in a row of a column <paramref name="column"/> wide, past
        /// which the controls wrap under each other.
        /// </summary>
        /// <remarks>
        /// A row puts its control in an Auto column, which WPF measures at infinite width, so a WrapPanel
        /// there never wraps unless it is told how wide it may be; unwrapped, a face's Quick glance needs
        /// about 400 px, and on a narrow column the title beside it was squeezed to a word a line.
        /// </remarks>
        public static double ControlsWidth(double column)
        {
            return Math.Max(0, column - PanelShell.RowGap - RowTitleLeast);
        }

        /// <summary>The gap a line of controls leaves before each control, which is how far apart they sit.</summary>
        public const double WrapGap = 8;

        /// <summary>The widest a binding chip is drawn beside a glance's choices, cut short past it with the whole
        /// binding in its hover: "FANATEC Podium Wheel Base DD1 · 12" is about 213 px at the chip's 13.</summary>
        public const double GlanceChipMax = 240;

        /// <summary>The widest line of controls the page draws in a row: a face's or a pit wall's glance, its
        /// zone and page choices and the chip, each after its gap. The companion's glance (one choice of at
        /// least GlancePageWidth and the chip) and the portrait wall's four choices are narrower.</summary>
        public const double ControlsMost = GlanceZoneWidth + GlancePageWidth + GlanceChipMax + 3 * WrapGap;

        /// <summary>The column past which no line of controls on the page wraps: the widest line, the row's
        /// gap and its title's least. A width read for a wrap stops here (ContentWidthUpTo).</summary>
        public const double ControlsColumnMost = ControlsMost + PanelShell.RowGap + RowTitleLeast;

        /// <summary>A binding chip's hover: where a press goes (PanelBindings.ChipTooltip), led by the whole
        /// binding only where the chip is <paramref name="cut"/> short, so a long device name can still be read
        /// and a whole one is not said twice (voice.md: nothing the control already says).</summary>
        public static string ChipTooltip(string label, bool cut)
        {
            return !cut || string.IsNullOrEmpty(label) ? PanelBindings.ChipTooltip : label + Environment.NewLine + PanelBindings.ChipTooltip;
        }

        /// <summary>A zone's hover in the picture: its name, then the page it opens on and the button line where
        /// the cell cuts them short, "Zone A · Gear, speed, revs · Not bound"; the page calls this with null for
        /// a line drawn whole. The reference face's zone A has about 79 px inside at the artboard's own frame,
        /// and its default page is 112 px.</summary>
        public static string ZoneCellTooltip(string letter, string page, string buttonLine)
        {
            var parts = new[] { PanelFacePlan.ZoneLabel(letter), page, buttonLine };
            return string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
        }

        /// <summary>The info bar's hover in the picture: its name, then the fields of each end a narrow bar cuts
        /// short ("Air temperature · Track temperature" is 171 px in a 151 px end at the artboard's frame); the
        /// page calls this with null for an end drawn whole.</summary>
        public static string InfoBarTooltip(string left, string right)
        {
            var hover = InfoBarTitle;
            if (!string.IsNullOrEmpty(left)) hover += Environment.NewLine + "Left: " + left;
            if (!string.IsNullOrEmpty(right)) hover += Environment.NewLine + "Right: " + right;
            return hover;
        }

        /// <summary>The count at the head of a zone aside and of the companion's modules ("4 of 21").</summary>
        public const double HeadCountSize = 15;

        // --- The quick glance ---------------------------------------------------------------------------

        /// <summary>The two choices a glance is made with: its zone, then that zone's page, each as wide as its
        /// longest label; the portrait wall's four zone choices, four to a line.</summary>
        public const double GlanceZoneWidth = 110;

        public const double GlancePageWidth = 150;

        public const double PortraitChoiceWidth = 96;


        /// <summary>The zones a face's glance can borrow, in Contract.FaceZoneLetters order.</summary>
        public static string[] GlanceZoneLabels()
        {
            return Contract.FaceZoneLetters.Select(PanelFacePlan.ZoneLabel).ToArray();
        }

        /// <summary>The pages of one of those zones, and nothing it does not carry, on a face of the theme
        /// given: band D on a Porsche face lists the Porsche row after the house's eight.</summary>
        public static string[] GlancePageLabels(int zoneIndex, string theme = null)
        {
            return FacePages.For(Contract.FaceZoneLetters[zoneIndex], theme).Select(page => page.Name).ToArray();
        }

        /// <summary>
        /// The glance after its zone was changed: the same page where the new zone carries it -- zone A's
        /// Track and module 13 are one drawing -- and the new zone's first page otherwise.
        /// </summary>
        public static int GlanceWithZone(int glance, int zoneIndex, string theme = null)
        {
            var current = Contract.NormaliseQuickGlance(glance, theme);
            var from = Contract.FaceZoneLetters[Contract.QuickGlanceZone(current)];
            var id = FacePages.IdOf(from, Contract.QuickGlancePage(current), theme);
            var pages = FacePages.For(Contract.FaceZoneLetters[zoneIndex], theme);
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

        /// <summary>Details' Version for a dashboard that is installed but says no version, or whose sidecar
        /// could not be read: a value in sentence case, where Versioning.UnknownVersion is written to follow a
        /// name ("OpenDash (unknown version)").</summary>
        public const string VersionUnknown = "Unknown";

        /// <summary>The version Details prints for what DashboardInstaller.InstalledVersionFrom read: Not
        /// installed for none, <see cref="VersionUnknown"/> for an unknown one, and the version otherwise.</summary>
        public static string VersionShown(string read)
        {
            if (read == null) return NotInstalled;
            return string.Equals(read, Versioning.UnknownVersion, StringComparison.Ordinal) || read.Length == 0 ? VersionUnknown : read;
        }

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

        /// <summary>The box's hover while it is empty, saying what it sets; its whole address once it has one. Not
        /// "the page": on this editor a page is Race, Tower or Telemetry, and a zone's page.</summary>
        public const string WebViewEmptyTooltip = "Sets what the web view shows, from an http or https address.";

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
        /// <remarks>
        /// Only the zones a driver can set. Screens.dc.html's Race zones card draws a Board row with a
        /// Leaderboard choice above Zone A and Zone B, but the race page's board is fixed in pitwall.ts and has
        /// no Contract.PitWallZoneSlot, so there is no setting for a row to write, and a choice that changes
        /// nothing is not drawn. A departure recorded for Tim: if he wants the board configurable, it is a
        /// ticket, and until then a greyed row.
        /// </remarks>
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

        /// <summary>
        /// The modules First module offers, as indexes into Modules.All: the ones ticked in the rotation, and
        /// every module where none is.
        /// </summary>
        /// <remarks>
        /// Save moves CompanionStart forward to the next module the rotation has on, so a module that is off
        /// was offered only to be replaced by another the driver did not pick: Energy, off by default, stored
        /// Tyres.
        /// </remarks>
        public static int[] FirstModuleChoices(bool[] modules)
        {
            var on = Enumerable.Range(0, Modules.Count).Where(i => modules != null && i < modules.Length && modules[i]).ToArray();
            return on.Length > 0 ? on : Enumerable.Range(0, Modules.Count).ToArray();
        }

        /// <summary>A module's hover in the grid: its number and what it shows, "07 · ...".</summary>
        public static string ModuleTooltip(Module module)
        {
            if (module == null) return string.Empty;
            return module.Number.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + " · " + module.Description;
        }

        /// <summary>Where a wheel button that pages the companion is bound, after the device is opened.</summary>
        public static readonly string[] CompanionPagingCrumbs = { "Controls and events", "NextScreen" };

        /// <summary>The name of the page a pit wall's zone shows, which the picture draws in the zone: from the
        /// wide pages for a wide slot and the standard ones otherwise, read from that zone's own setting.</summary>
        public static string PitWallZonePageName(ScreenInstance screen, Contract.PitWallZoneSlot slot)
        {
            if (screen == null || slot == null) return string.Empty;
            var page = screen.ZonePage(slot.Key);
            return slot.Wide ? ZonePages.WideName(page) : ZonePages.StandardName(page);
        }

        // --- A round screen ------------------------------------------------------------------------------

        /// <summary>The round screen's rev ring, which is the rig-wide rev bar.</summary>
        public const string RevRingTitle = "Rev ring";

        /// <summary>The rig-wide rev row's title on this card face: a ring on a round screen, and the rev bar
        /// a rectangular card face draws across its top.</summary>
        public static string RevRingTitleFor(ScreenInstance screen)
        {
            return screen == null || IsRound(screen) ? RevRingTitle : RevBarTitle;
        }

        /// <summary>The round pane's search label, drawn as its Card rows.</summary>
        public const string CardsTitle = "Cards";

        public const string CardsCaption = "Cards are shared by every round screen.";

        /// <summary>Whether <paramref name="rig"/> holds a card face that is not round, which reads the same
        /// shared cards and rig-wide rev bar as the round screens.</summary>
        public static bool HasCardFace(IEnumerable<ScreenInstance> rig)
        {
            return rig != null && rig.Any(screen => screen != null && screen.IsSlots && !IsRound(screen));
        }

        /// <summary>
        /// The caption under the cards, naming every screen that shares them: the round screens, and the card
        /// faces where the rig holds one. A card face's own editor told its driver the cards belonged to round
        /// screens; a rig with none is not told about a kind it cannot add.
        /// </summary>
        public static string CardsCaptionFor(IEnumerable<ScreenInstance> rig)
        {
            return HasCardFace(rig) ? "Cards are shared by every round screen and card face." : CardsCaption;
        }

        public static string CardLabel(int slot)
        {
            return "Card " + slot;
        }

        /// <summary>The card a slot holding <paramref name="value"/> shows, by name: a value past either end of
        /// Cards.All is the nearest card, as the slot's choice reads it.</summary>
        public static string CardShown(int value)
        {
            return Cards.All[Math.Max(0, Math.Min(Cards.All.Count - 1, value))].DisplayName;
        }

        /// <summary>A card's hover on the disc, where its name is cut short: which card it is, and what it shows.</summary>
        public static string CardHover(int slot, string name)
        {
            return CardLabel(slot) + " · " + name;
        }

        /// <summary>
        /// How many slots each card face's layout reads, by its size: packages/dash/src/layouts, which writes
        /// the count into each package's description ("1280 x 480, 8 slots"). PanelScreensTests holds this
        /// table to the descriptions the build's snapshot records.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> CardsReadBySize = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "1920x480", 12 },
            { "1280x480", 8 },
            { "1280x400", 8 },
            { "850x480", 6 },
            { "800x480", 6 },
            { "1280x720", 12 },
            { "800x286", 4 },
            { "600x686", 6 },
            { "480x480", 2 },
            { "800x800", 6 },
        };

        /// <summary>
        /// How many slots the screen's package reads: the 480 round two, the 800 round six, and a rectangular
        /// card face what its layout reads -- eight at 1280 x 480, not twelve. A size no layout has reads all
        /// twelve, the most any can.
        /// </summary>
        /// <remarks>
        /// Every rectangular face read twelve here, so its editor drew twelve Card rows, warned of clashes in
        /// slots the screen never draws (the fault the 480 round had), and Details named Slot01 to Slot12.
        /// </remarks>
        public static int CardsRead(ScreenInstance screen)
        {
            if (screen == null) return Contract.SlotCount;
            int read;
            return CardsReadBySize.TryGetValue(screen.Width + "x" + screen.Height, out read) ? read : Contract.SlotCount;
        }

        /// <summary>Whether the round screen's editor draws the disc with its cards on it: a round screen whose
        /// cards fit the disc. A rectangular card face is drawn as its rows alone, as it is not a disc.</summary>
        public static bool DrawsDisc(ScreenInstance screen)
        {
            return IsRound(screen) && PanelRoundPlan.OnDisc(CardsRead(screen));
        }

        /// <summary>
        /// The line under a round screen's cards when two of them show the same thing: "Speed is on Card 1
        /// and Card 2.", one line per repeated card, and nothing when there is none.
        /// </summary>
        /// <remarks>
        /// Only over the cards this screen reads. The twelve slots are the rig's, so a 480 round warned over
        /// all of them named slots it never draws and no control on its pane can change, and it said "slots"
        /// beside rows called Card 1 and Card 2. A slot's 1-based position is its card's number.
        /// </remarks>
        public static string CardClash(int[] slots, int read)
        {
            if (slots == null) return string.Empty;
            var lines = DuplicateAssignment.Find(slots.Take(Math.Max(0, read)).ToArray()).Select(duplicate =>
            {
                var cards = duplicate.Slots.Select(CardLabel).ToArray();
                var list = string.Join(", ", cards.Take(cards.Length - 1)) + " and " + cards[cards.Length - 1];
                return Cards.DisplayName(duplicate.Card) + " is on " + list + ".";
            });
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// The caption under the round pane's Rev ring, which writes the rig-wide OpenDash.RevBar. It reaches
        /// further than the round screens: every face whose own rev bar was never set reads it, since
        /// ScreenRevBar falls back to the rig's while a screen's is null and a new face starts null; and the
        /// Speedo module reads it wherever it is drawn -- the companion's, and a face's zone B or C, whose
        /// Speedo page reads OpenDash.RevBar and never the face's own. "Applies to every round face on your
        /// rig." let a driver turn off the main face's rev bar from the round pane unawares, and "Every round
        /// screen and the phone's speedo." left those faces out. A page name is lower case after an article,
        /// as the clash lines write them, and the list has no serial comma, as the page's others do not, so the
        /// relative clause stands last: before "and the speedo" it read as though the speedo were something
        /// the driver had not set, and straight after "card face" as a clause saying which card faces count.
        /// </summary>
        public const string RigRevBarCaption = "Every round screen, the speedo wherever it is shown and any face whose own rev bar you have not set.";

        /// <summary>The Rev ring's or Rev bar's caption on <paramref name="rig"/>: <see cref="RigRevBarCaption"/>,
        /// naming the card faces beside the round screens where the rig holds one, since a card face's arc reads
        /// the same rig-wide setting and its own editor draws this row.</summary>
        public static string RigRevBarCaptionFor(IEnumerable<ScreenInstance> rig)
        {
            return HasCardFace(rig)
                ? "Every round screen and card face, the speedo wherever it is shown and any face whose own rev bar you have not set."
                : RigRevBarCaption;
        }

        // --- Anchors, search and the greyed rows ---------------------------------------------------------

        public const string AnchorCards = "screens.cards";

        /// <summary>The dashed tile that adds a screen. A route to it opens the Add sheet once the page is drawn
        /// (#523): Home's empty-rig tile goes there rather than calling the page's sheet itself.</summary>
        public const string AnchorAdd = "screens.add";
        public const string AnchorRevBar = "screens.revbar";
        public const string AnchorFlagDisplay = "screens.flag-display";
        public const string AnchorLapReview = "screens.lap-review";
        /// <summary>The picture of a face's zones, and of a landscape pit wall's page: both draw their zones.</summary>
        public const string AnchorZones = "screens.zones";

        /// <summary>A face's Info bar aside, which opens on it: only a face with a bar draws it.</summary>
        public const string AnchorInfoBar = "screens.info-bar";

        /// <summary>The Next page and Previous page chips at the foot of a face's zone aside.</summary>
        public const string AnchorZonePaging = "screens.zone-paging";
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
            // The dashed tile's own words, and its route, which opens the Add sheet as Home's empty-rig tile does.
            new PanelSearch.Entry(PanelAddScreen.SectionTitle, PanelPage.Screens, AnchorAdd, "add screen", "new", "dashboard", "display"),
            new PanelSearch.Entry(RevBarTitle, PanelPage.Screens, AnchorRevBar, "shift lights", "revbar", "rpm"),
            new PanelSearch.Entry(FlagDisplayTitle, PanelPage.Screens, AnchorFlagDisplay, "band d", "full screen", "flags"),
            new PanelSearch.Entry(LapReviewTitle, PanelPage.Screens, AnchorLapReview, "last lap"),
            new PanelSearch.Entry(ZonesTitle, PanelPage.Screens, AnchorZones, "pages", "zone a", "zone b", "zone c", "band d", "reorder"),
            new PanelSearch.Entry(InfoBarTitle, PanelPage.Screens, AnchorInfoBar, "bar", "race time", "position"),
            new PanelSearch.Entry(NextPageTitle, PanelPage.Screens, AnchorZonePaging, "zone", "button"),
            new PanelSearch.Entry(PreviousPageTitle, PanelPage.Screens, AnchorZonePaging, "zone", "back", "button"),
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

        // --- Which screen a route to a row opens ----------------------------------------------------------

        /// <summary>The greyed rows under a face's own rows, in the order Screens.Face draws them.</summary>
        private static readonly SoonItem[] FaceSoon =
        {
            PanelSoon.RevFill,
            PanelSoon.SpotterAtRevBarEnds,
            PanelSoon.PitPageInPitLane,
            PanelSoon.PopUps,
            PanelSoon.DeltaEdgeLights,
            PanelSoon.ScreenCare,
            PanelSoon.Fit,
        };

        /// <summary>The greyed pages a zone list draws under Show all, in zone B or C.</summary>
        private static readonly SoonItem[] ZoneListSoon = { PanelSoon.CircleTracker, PanelSoon.Launch };

        /// <summary>
        /// Whether the editor of <paramref name="screen"/> draws the row <paramref name="anchor"/> names: a row
        /// is drawn only under a screen of the kind that has it, so a search hit has to open such a screen
        /// before it can scroll to the row. False for an anchor that is not this page's.
        /// </summary>
        public static bool Draws(string anchor, ScreenInstance screen)
        {
            if (screen == null || anchor == null) return false;
            var face = screen.IsFace && screen.FaceSize != null;
            var landscapeWall = screen.IsPitWall && !IsPortrait(screen);
            switch (anchor)
            {
                case AnchorCards:
                case AnchorAdd:
                case AnchorDetails:
                    return true;
                case AnchorZones:
                    return face || landscapeWall;
                case AnchorInfoBar:
                    return face && screen.FaceSize.Value.HasBar;
                case AnchorRevBar:
                case AnchorLapReview:
                case AnchorZonePaging:
                    return face;
                case AnchorFlagDisplay:
                    return face || screen.IsPitWall || screen.IsCompanion;
                case AnchorGlance:
                    return face || landscapeWall || screen.IsCompanion;
                case AnchorClassOnly:
                    return face || screen.IsPitWall;
                case AnchorPitWallPage:
                    return landscapeWall;
                case AnchorWebView:
                    return screen.IsPitWall;
                case AnchorPortrait:
                    return screen.IsPitWall && IsPortrait(screen);
                case AnchorModules:
                case AnchorFirstModule:
                case AnchorPaging:
                    return screen.IsCompanion;
                case AnchorSlots:
                    return screen.IsSlots;
                case AnchorRevRing:
                    // A round screen's: a rectangular card face titles the same rig-wide row Rev bar, so a hit
                    // on "Rev ring" opens a round screen, where the row it lands on carries the name searched.
                    return IsRound(screen);
            }
            if (string.Equals(anchor, PanelSoon.ZonesInsteadOfCards.Anchor, StringComparison.Ordinal)) return screen.IsSlots;
            if (FaceSoon.Concat(ZoneListSoon).Any(item => string.Equals(item.Anchor, anchor, StringComparison.Ordinal))) return face;
            return false;
        }

        /// <summary>
        /// The screen a route to <paramref name="anchor"/> opens: the selected one where it draws the row, the
        /// first on the rig that does otherwise, and the selected one when none does -- the route then lands
        /// on the page's top, which is the cards.
        /// </summary>
        public static ScreenInstance ScreenFor(string anchor, IEnumerable<ScreenInstance> rig, ScreenInstance selected)
        {
            if (anchor == null || Draws(anchor, selected)) return selected;
            var drawing = rig == null ? null : rig.FirstOrDefault(screen => Draws(anchor, screen));
            return drawing ?? selected;
        }

        /// <summary>Whether search lists a row of this page on <paramref name="rig"/>: the cards and the add tile
        /// on any rig, every other row only where a screen of the rig draws it (Draws), which is the screen
        /// ScreenFor then opens.</summary>
        public static bool SearchDrawn(string anchor, IEnumerable<ScreenInstance> rig)
        {
            if (anchor == null || anchor == AnchorCards || anchor == AnchorAdd) return true;
            return rig != null && rig.Any(screen => Draws(anchor, screen));
        }

        /// <summary>
        /// The zone a face's aside has to show for the row <paramref name="anchor"/> names to be drawn:
        /// the bar for the Info bar, a zone for Next page and Previous page, B or C for the two greyed pages,
        /// a zone with a class filter for My class only, and whatever was picked for every other row.
        /// </summary>
        public static string AsideFor(string anchor, string picked, Contract.FaceSize face)
        {
            var key = AsideKey(picked, face);
            if (string.Equals(anchor, AnchorInfoBar, StringComparison.Ordinal)) return face.HasBar ? BarKey : picked;
            if (string.Equals(anchor, AnchorZonePaging, StringComparison.Ordinal)) return key == BarKey ? FirstAside : picked;
            if (ShowsEveryPage(anchor)) return ListsSoonModules(key) ? key : FirstAside;
            if (string.Equals(anchor, AnchorClassOnly, StringComparison.Ordinal)) return FacePages.OffersClassFilter(key) ? key : FirstAside;
            return picked;
        }

        /// <summary>Whether the row is drawn only under Show all: the two greyed pages of zones B and C.</summary>
        public static bool ShowsEveryPage(string anchor)
        {
            return ZoneListSoon.Any(item => string.Equals(item.Anchor, anchor, StringComparison.Ordinal));
        }

        /// <summary>Search labels this page draws through something other than the constant: the zones as
        /// their letters, and the round screen's cards as one row per slot.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>
        {
            { ZonesTitle, "PanelFacePlan.ZoneLabel(" },
            { CardsTitle, "PanelScreens.CardLabel(" },
        };
    }
}
