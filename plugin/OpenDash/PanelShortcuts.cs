// PanelShortcuts.cs: the Shortcuts page -- its words, its rows, its filter and its clash line, drawn by
// SettingsControl.Shortcuts.cs to Shortcuts.dc.html.
//
// Every wheel button and key in one list, one card per screen and one for the rig's lights: a zone's next and
// previous page, the quick glance, night mode, brightness up and down. The binding control in each row is
// SimHub's own ControlsEditor, so the page draws rows around it rather than a binder of its own; what this
// file decides is which rows a card has and in what order, what each says, which the filter shows, how a card
// counts what is bound, and the sentence that names a button bound to two things. The Shortcuts page agent
// owns this file. Pure: no WPF and no SimHub types. PanelShortcutsTests holds it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenDashPlugin
{
    public static class PanelShortcuts
    {
        public const string Title = "Shortcuts";

        public const string AnchorScreens = "shortcuts.screens";
        public const string AnchorRig = "shortcuts.rig";
        public const string AnchorAlerts = "shortcuts.alerts";

        /// <summary>The line under the title, as ruled: what can be bound, and nothing the rows already show.</summary>
        public const string IntroCaption = "A wheel button, a button box or a key.";

        /// <summary>The card the rig's own actions are listed in, and the line beside its name.</summary>
        public const string RigGroupTitle = "Lights";
        public const string RigGroupDetail = "Every strip and matrix";

        /// <summary>The card of what an alert on screen can be bound to. Only greyed rows today.</summary>
        public const string AlertsGroupTitle = "Alerts";


        // The rows' own words: a face's rows read "Band D · next page", and every kind's glance row reads
        // "Quick glance".
        public const string NextPageTitle = "Next page";
        public const string PreviousPageTitle = "Previous page";
        public const string QuickGlanceTitle = "Quick glance";

        /// <summary>The press a row's action answers to, in the column beside its name.</summary>
        public const string Tap = "Tap";
        public const string Hold = "Hold";

        /// <summary>
        /// Whether a row draws its press word: while nothing is bound on it, and always on a held row. Once a
        /// row is bound, SimHub's editor prints each binding's own press type beside it, and that is the
        /// statement: a zone bound as a long press would otherwise read "Tap" beside SimHub's LongPress, and
        /// where the control already says it the panel says nothing. A held row keeps "Hold", because the
        /// plugin holds its glance whatever press type SimHub shows (#435), which its caption says.
        /// </summary>
        public static bool ShowsPress(RowState row, bool hold)
        {
            return hold || row != RowState.Bound;
        }

        /// <summary>A zone's row, and the name BuildBinder's fallback text gives the action when SimHub's editor
        /// cannot be drawn, in the same words: "Band D · next page", "Rim · Band D · previous page". SimHub's
        /// editor itself is given <see cref="EditorName"/>.</summary>
        public static string ZoneRow(string zoneLabel, bool next)
        {
            return zoneLabel + Join + (next ? NextPageTitle : PreviousPageTitle).ToLowerInvariant();
        }

        public static string ZoneBinderName(string screenName, string zoneLabel, bool next)
        {
            return screenName + Join + ZoneRow(zoneLabel, next);
        }

        /// <summary>The glance's name in BuildBinder's fallback text, the row's own label after the screen's
        /// name, as a zone's is: "Rim · Quick glance". A value is written the way its control names it.</summary>
        public static string GlanceBinderName(string screenName)
        {
            return screenName + Join + QuickGlanceTitle;
        }

        private const string Join = " · ";

        // --- Rows ---------------------------------------------------------------------------------------

        /// <summary>
        /// One row of a card: the action it binds, its label, the name BuildBinder's fallback text gives it,
        /// the press it answers to, whether it carries the New tag, and what it does in the clash line's words.
        /// </summary>
        public sealed class Binding
        {
            public Binding(string action, string label, string binderName, bool hold, bool isNew, string does)
            {
                Action = action;
                Label = label;
                BinderName = binderName;
                IsHold = hold;
                IsNew = isNew;
                Does = does;
            }

            public string Action { get; private set; }
            public string Label { get; private set; }
            public string BinderName { get; private set; }
            public bool IsHold { get; private set; }
            public bool IsNew { get; private set; }
            public string Does { get; private set; }

            /// <summary>"Tap" or "Hold".</summary>
            public string Press { get { return IsHold ? Hold : Tap; } }
        }

        /// <summary>
        /// A face's zone rows, in the order Shortcuts.dc.html lists them: every zone's next page, Zone A to
        /// Band D, then every zone's previous page (new in this release). The glance row, which is the card's
        /// last, is <see cref="GlanceBinding"/>, because its binder is the one that holds.
        /// </summary>
        /// <remarks>
        /// Contract.FaceZoneLetters' order for every face, whatever its size: the list reads by letter, as the
        /// artboard's does, where the face picture on Screens draws a Row face's zones B, A, C
        /// (PanelFacePlan.ZoneOrder). So two faces on one rig list their rows alike, and a face whose size
        /// OpenDash does not know lists every zone as well.
        /// </remarks>
        public static IList<Binding> FaceBindings(string ns, string screenName)
        {
            var zoneOrder = Contract.FaceZoneLetters;
            var rows = new List<Binding>();
            foreach (var letter in zoneOrder)
            {
                var zone = PanelFacePlan.ZoneLabel(letter);
                rows.Add(new Binding(Contract.CycleZoneAction(ns, letter), ZoneRow(zone, true), ZoneBinderName(screenName, zone, true), false, false, ZoneDoes(zone, true)));
            }
            foreach (var letter in zoneOrder)
            {
                var zone = PanelFacePlan.ZoneLabel(letter);
                rows.Add(new Binding(Contract.CycleZoneBackAction(ns, letter), ZoneRow(zone, false), ZoneBinderName(screenName, zone, false), false, true, ZoneDoes(zone, false)));
            }
            return rows;
        }

        /// <summary>
        /// The quick glance's row, the same on a face, a pit wall and a companion: held. New on a companion
        /// alone, by the rule that marks what the shipped plugin cannot do (Delta precision, Clock, Night
        /// mode). v0.3.0-rc.7 did register a companion's glance, whatever its CompanionActionNames held
        /// (OpenDash.AttachActions' screen loop), but the action moved a property no package read (#435)
        /// and that release's panel had no binder for it; the glance works from #362, after that cut, so
        /// this release is the first where binding it does anything. A driver may already have an rc.7
        /// binding on the row, which now starts to work. A face's and a pit wall's glance worked in rc.7
        /// and carry no tag.
        /// </summary>
        public static Binding GlanceBinding(string kind, string ns, string screenName)
        {
            var isNew = string.Equals(kind, Contract.KindCompanion, StringComparison.Ordinal);
            return new Binding(Contract.HoldQuickGlanceActionFor(ns), QuickGlanceTitle, GlanceBinderName(screenName), true, isNew, GlanceDoes);
        }

        /// <summary>
        /// Whether a pit wall has a glance row, and so a card: not when it stands in portrait, taller than it
        /// is wide, as the Screens page tells the two apart. The glance swaps the page of one of the
        /// landscape zones (Contract.GlanceZoneSlots, ScreenInstance.BeginQuickGlance), and the portrait
        /// package draws only its own four, so a held key would change nothing on screen. A wall whose
        /// size is not known keeps the row.
        /// </summary>
        public static bool PitWallGlances(int width, int height)
        {
            return height <= width || width <= 0 || height <= 0;
        }

        /// <summary>
        /// Whether a pit wall has a card: when its glance works, and on a portrait wall while its glance is
        /// still bound, or cannot be read. Contract.PitWallActionNames registers the glance whatever the
        /// wall's orientation, so a binding made while the wall stood in landscape, or in SimHub's Controls
        /// and events, stays live in SimHub and in the sidebar's Shortcuts count; the card is where it is
        /// seen and cleared. So the cards' bound rows add up to the sidebar's count, which counts only what
        /// is bound: a portrait wall's glance with nothing on it is counted by neither.
        /// </summary>
        public static bool PitWallCard(int width, int height, bool glanceBound)
        {
            return PitWallGlances(width, height) || glanceBound;
        }

        /// <summary>
        /// The caption a portrait wall's glance row carries in place of PanelCopy.PitWallGlance, which would
        /// say it puts a zone back where the glance does nothing. It names the fact in the panel's words
        /// ("Pit wall", as PanelAddScreen.KindName has it) and not the mechanism, and it ends on
        /// PanelCopy.GlanceBoundAsHold as every glance row's caption does (#435): the row is still a hold
        /// binder, which turns any press type picked on it back into a hold.
        /// </summary>
        public const string PortraitGlanceCaption = "Does nothing on a portrait pit wall. " + PanelCopy.GlanceBoundAsHold;

        /// <summary>
        /// The Lights card's live rows: night mode, and brightness up and down. All three are new: no released
        /// plugin registers any of them (v0.3.0-rc.7 has none), and New marks what the shipped plugin cannot
        /// do, as ruled for Delta precision and Clock. Shortcuts.dc.html draws Night mode untagged.
        /// </summary>
        public static IList<Binding> LightsBindings()
        {
            return Contract.RigActionNames()
                .Select(action => new Binding(action, RigActionLabel(action), RigActionLabel(action), false, true, RigActionDoes(action)))
                .ToList();
        }

        /// <summary>The friendly name a binder gives one of the rig's own actions (Contract.RigActionNames).</summary>
        public static string RigActionLabel(string action)
        {
            switch (action)
            {
                case Contract.ToggleNightModeAction: return "Night mode";
                case Contract.BrightnessUpAction: return "Brightness up";
                case Contract.BrightnessDownAction: return "Brightness down";
                default: return action;
            }
        }

        // --- The card's header ---------------------------------------------------------------------------

        /// <summary>
        /// The line beside a screen card's name: its kind and its size, as a Screens card writes them
        /// ("Face · 1280 × 480", "Pit wall · 1920 × 1080"), less whatever the name already says. A pit wall
        /// named "Pit wall" reads "1920 × 1080", a face named by its size reads "Face", and a line with nothing
        /// left is null. The kind alone when the size is not known.
        /// </summary>
        public static string GroupDetail(string screenName, string kind, int width, int height)
        {
            var kindName = PanelAddScreen.KindName(kind);
            var size = width > 0 && height > 0
                ? width.ToString(CultureInfo.InvariantCulture) + " × " + height.ToString(CultureInfo.InvariantCulture)
                : null;
            var name = (screenName ?? string.Empty).Trim();
            var parts = new List<string>();
            if (!string.Equals(name, kindName, StringComparison.OrdinalIgnoreCase)) parts.Add(kindName);
            if (size != null && !string.Equals(name, size, StringComparison.Ordinal)) parts.Add(size);
            return parts.Count == 0 ? null : string.Join(Join, parts);
        }

        /// <summary>What a row is, for the filter and its card's count: greyed (it cannot be bound yet), not
        /// read (SimHub's mappings could not be read for it), not bound, or bound.</summary>
        public enum RowState
        {
            Greyed,
            Unread,
            NotBound,
            Bound,
        }

        /// <summary>A row's state from whether it can be bound and how many bindings were read for it, null
        /// when none could be.</summary>
        public static RowState StateOf(bool bindable, int? bindings)
        {
            if (!bindable) return RowState.Greyed;
            if (bindings == null) return RowState.Unread;
            return bindings.Value > 0 ? RowState.Bound : RowState.NotBound;
        }

        /// <summary>
        /// Whether the page shows what it read: the filter, the counts and the clash line. Not when any row
        /// could not be read, as ruled: a count that is sometimes wrong would be worse than none.
        /// </summary>
        public static bool Readable(IEnumerable<RowState> rows)
        {
            return (rows ?? Enumerable.Empty<RowState>()).All(row => row != RowState.Unread);
        }

        /// <summary>
        /// A card's count, "3 of 6": how many of the rows it can bind are bound. Null when the page is not
        /// <see cref="Readable"/>, and for a card with nothing to bind.
        /// </summary>
        /// <remarks>
        /// Greyed rows are not counted, since no press could ever complete the count: the artboard counts them
        /// ("1 of 4" on Lights, "0 of 1" on Alerts), and it was ruled to leave them out. The companion's card
        /// counts its glance like any other, which departs from the artboard: Shortcuts.dc.html gives its
        /// external Phone card a Quick glance row and still blanks that card's count. The page shows it so
        /// that the cards add up to the sidebar's total, which counts the companion's glance; the
        /// departure is listed for the author to rule on.
        /// </remarks>
        public static string CardCount(IEnumerable<RowState> rows, bool readable)
        {
            if (!readable) return null;
            var list = (rows ?? Enumerable.Empty<RowState>()).ToList();
            var total = list.Count(row => row != RowState.Greyed);
            if (total <= 0) return null;
            var bound = list.Count(row => row == RowState.Bound);
            return bound.ToString(CultureInfo.InvariantCulture) + " of " + total.ToString(CultureInfo.InvariantCulture);
        }

        // --- The companion's paging ---------------------------------------------------------------------

        public const string ControlsAndEventsCrumb = "Controls and events";
        public const string NextScreenCrumb = "NextScreen";

        /// <summary>
        /// Where SimHub binds a companion's paging, under PanelCopy.CompanionPaging, which says where to start
        /// ("open the device or window the companion runs on in SimHub"): Controls and events › NextScreen,
        /// the two names SimHub itself gives. The brief's trail began "Devices › Phone", the card's own name,
        /// but that is OpenDash's name for the screen, not a device SimHub lists, and a companion in a window
        /// sits under no Devices at all, so a crumb for it could name a row SimHub does not have. The Screens
        /// page draws the same two (PanelScreens.CompanionPagingCrumbs), so one binding has one path.
        /// </summary>
        public static readonly string[] PagingCrumbs = { ControlsAndEventsCrumb, NextScreenCrumb };

        // --- The filter ---------------------------------------------------------------------------------

        public const string FilterAll = "all";
        public const string FilterBound = "bound";
        public const string FilterNotBound = "not-bound";

        public static readonly string[] FilterValues = { FilterAll, FilterBound, FilterNotBound };
        public static readonly string[] FilterLabels = { "All", "Bound", PanelBindings.NotBound };

        /// <summary>The filter's name, read by a screen reader alone, since the artboard draws no words over it.
        /// A noun, as voice.md has every label: the artboard's aria-label is the bare verb "Show", and a verb
        /// is a button's, as ruled where "Centre shows" became "Centre display".</summary>
        public const string FilterTitle = "Filter";

        /// <summary>
        /// Whether a row shows under the filter. A greyed row is not bound, and its chip says so, so it shows
        /// under Not bound as well as All, where its Soon tag says why; that keeps "Every shortcut is bound."
        /// true, which is why the line cannot appear while the Rig test (#511) and the alert's dismissal
        /// (#510) are drawn greyed. A row not read shows under All alone, and FilterFor gives All whenever one is.
        /// </summary>
        public static bool Shows(string filter, RowState row)
        {
            if (filter == FilterBound) return row == RowState.Bound;
            if (filter == FilterNotBound) return row == RowState.NotBound || row == RowState.Greyed;
            return true;
        }

        /// <summary>
        /// The filter a build draws: the one chosen earlier in the session, unless the page was opened on a
        /// row (a binding chip's press, a search hit), which the filter must not hide, or SimHub's mappings
        /// cannot be read, when there is no filter at all. The page asks with the anchor on the first build
        /// after it is opened alone: a rebuild in place keeps the route, anchor and all, and the choice.
        /// </summary>
        public static string FilterFor(string chosen, string anchor, bool readable)
        {
            if (!readable || !string.IsNullOrEmpty(anchor)) return FilterAll;
            return Array.IndexOf(FilterValues, chosen) >= 0 ? chosen : FilterAll;
        }

        /// <summary>The cards in the page's order: the screens', then Lights, then Alerts.</summary>
        public static IList<T> CardOrder<T>(IEnumerable<T> screens, T lights, T alerts)
        {
            return (screens ?? Enumerable.Empty<T>()).Concat(new[] { lights, alerts }).ToList();
        }

        /// <summary>Whether a card shows under the filter: only with a row to show, never as a header alone.</summary>
        public static bool CardShows(IEnumerable<RowState> rows, string filter)
        {
            return (rows ?? Enumerable.Empty<RowState>()).Any(row => Shows(filter, row));
        }

        /// <summary>Whether a shown row draws the rule above it: every one but the first under its card's
        /// header, which draws a rule under itself, unless a line (the companion's paging) sits between.</summary>
        public static bool RuleAbove(bool firstShown, bool hasLead)
        {
            return !firstShown || hasLead;
        }

        /// <summary>The line under the cards: what <see cref="FilterEmpty"/> says, and only when the filter
        /// leaves no row on the page.</summary>
        public static string EmptyLine(string filter, bool anyShown)
        {
            return anyShown ? null : FilterEmpty(filter);
        }

        /// <summary>What the page says when the filter leaves no row: null under All, which always has one.</summary>
        public static string FilterEmpty(string filter)
        {
            if (filter == FilterBound) return "Nothing is bound yet.";
            if (filter == FilterNotBound) return "Every shortcut is bound.";
            return null;
        }

        // --- The clash line -----------------------------------------------------------------------------

        /// <summary>
        /// Which presses of its trigger a binding answers. SimHub reports a press three ways: when it goes
        /// down, when it comes up, and once as either a short or a long press (PluginManager.TriggerInput,
        /// 9.12.6), and a binding fires only on the presses its press type matches (CheckPressType). So a
        /// short press and a long press of one button are two gestures, and two bindings on them never fire
        /// together; every other press type (Default, During, ShortAndLongPress, Pressed, Released and the
        /// latching ones) answers every press.
        /// </summary>
        public enum Fires
        {
            OnEveryPress,
            OnShortPress,
            OnLongPress,
        }

        /// <summary>The presses a binding answers, from the name of SimHub's PressType it carries. The view
        /// passes <c>mapping.PressType.ToString()</c>, so this file stays free of SimHub's types.</summary>
        public static Fires FiresOn(string pressType)
        {
            switch (pressType)
            {
                case "ShortPress": return Fires.OnShortPress;
                case "LongPress":
                case "LongPressNoAutoRepeat": return Fires.OnLongPress;
                default: return Fires.OnEveryPress;
            }
        }

        /// <summary>One binding of one trigger: the raw trigger SimHub stores, the card it is on (a screen's
        /// name, or null for the rig's lights), what the row does, in the clash line's words, and the presses
        /// of the trigger it answers.</summary>
        public sealed class BindingUse
        {
            public BindingUse(string trigger, string place, string does, Fires fires = Fires.OnEveryPress)
            {
                Trigger = trigger;
                Place = place;
                Does = does;
                Fires = fires;
            }

            public string Trigger { get; private set; }
            public string Place { get; private set; }
            public string Does { get; private set; }
            public Fires Fires { get; private set; }
        }

        /// <summary>A trigger bound to more than one row: its name, drawn strong, the rest of the line, and
        /// the uses it names, whose rows the page marks.</summary>
        public sealed class Clash
        {
            public Clash(string trigger, string lead, string rest, IList<BindingUse> uses = null)
            {
                Trigger = trigger;
                Lead = lead;
                Rest = rest;
                Uses = uses ?? new List<BindingUse>();
            }

            public string Trigger { get; private set; }
            public string Lead { get; private set; }
            public string Rest { get; private set; }
            public IList<BindingUse> Uses { get; private set; }
            public string Text { get { return Lead + " " + Rest; } }
        }

        /// <summary>
        /// Whether a row is one a clash line names, by the card it is on and what it does: the artboard's
        /// "Row bound with clash", whose binder is outlined in caution (.key.clash), so the driver finds
        /// the doubled rows without reading them out of the line. Main dash's and Rim's zone B for "CSL
        /// Elite · 7 cycles zone B on both Main dash and Rim.", and no other row.
        /// </summary>
        public static bool Marks(IEnumerable<Clash> clashes, string place, string does)
        {
            return (clashes ?? Enumerable.Empty<Clash>())
                .Where(clash => clash != null)
                .SelectMany(clash => clash.Uses)
                .Any(use => use != null && use.Place == place && use.Does == does);
        }

        /// <summary>
        /// What a zone's row does, in the clash line, with the zone in its running-text form: "cycles zone B",
        /// as the artboard's line has it for a next page, and "takes zone D to its previous page", in the
        /// row's own words for the other. A label starts with a capital because it starts a label ("Zone B ·
        /// next page", "Band D · next page", the Screens picture); mid-sentence the panel writes every zone
        /// "zone" and its letter, D included, as the Screens page's own warning does for the same face
        /// (FacePageClash.Message: "Zone A and zone D both show the relative."). voice.md names "band D" in
        /// running text as the panel talking to itself, so one zone is not given two names on two pages.
        /// </summary>
        public static string ZoneDoes(string zoneLabel, bool next)
        {
            var zone = Running(zoneLabel);
            return next ? "cycles " + zone : "takes " + zone + PreviousPageTail;
        }

        /// <summary>The previous page's words after its zone, and the form two or more zones share:
        /// "takes zone A and zone D to their previous pages".</summary>
        private const string PreviousPageTail = " to its previous page";
        private const string PreviousPagesTail = " to their previous pages";

        /// <summary>A zone's label as it reads mid-sentence: "zone" and its letter, the last word of the
        /// label ("Zone B" and "Band D" read "zone B" and "zone D"). A label of one word is lower-cased at its
        /// first letter alone.</summary>
        private static string Running(string label)
        {
            if (string.IsNullOrEmpty(label)) return label ?? string.Empty;
            var space = label.Trim().LastIndexOf(' ');
            if (space < 0) return char.ToLowerInvariant(label[0]) + label.Substring(1);
            return "zone " + label.Trim().Substring(space + 1);
        }

        public const string GlanceDoes = "holds the quick glance";

        /// <summary>What one of the rig's own rows does, in the clash line.</summary>
        public static string RigActionDoes(string action)
        {
            switch (action)
            {
                case Contract.ToggleNightModeAction: return "toggles night mode";
                case Contract.BrightnessUpAction: return "turns the brightness up";
                case Contract.BrightnessDownAction: return "turns the brightness down";
                default: return action;
            }
        }

        /// <summary>
        /// Every trigger bound to more than one row, in the order the page first meets it, each with the line
        /// that says what it does where: "CSL Elite · 7 cycles zone B on both Main dash and Rim." It says what
        /// is doubled and nothing more, since doubling a button on purpose is a fair thing to do.
        /// </summary>
        /// <remarks>
        /// Triggers are SimHub's own strings and compared as they are stored; the line names one through
        /// PanelBindings.TriggerLabel, as every page names a binding. A use repeated on one card with the same
        /// action is one use, answering every press either copy answers. Two uses of one trigger are doubled
        /// only when one press can fire both (<see cref="Fires"/>): a button's short press on one row and its
        /// long press on another is two gestures, and no line. The line names every use that is doubled with
        /// another, in the page's order.
        /// </remarks>
        public static IList<Clash> Clashes(IEnumerable<BindingUse> uses)
        {
            var order = new List<string>();
            var byTrigger = new Dictionary<string, List<Heard>>(StringComparer.Ordinal);
            foreach (var use in uses ?? Enumerable.Empty<BindingUse>())
            {
                if (use == null || string.IsNullOrWhiteSpace(use.Trigger)) continue;
                List<Heard> list;
                if (!byTrigger.TryGetValue(use.Trigger, out list))
                {
                    list = new List<Heard>();
                    byTrigger.Add(use.Trigger, list);
                    order.Add(use.Trigger);
                }
                var onShort = use.Fires != Fires.OnLongPress;
                var onLong = use.Fires != Fires.OnShortPress;
                var seen = list.FirstOrDefault(heard => heard.Use.Place == use.Place && heard.Use.Does == use.Does);
                if (seen != null)
                {
                    seen.OnShort |= onShort;
                    seen.OnLong |= onLong;
                    continue;
                }
                list.Add(new Heard { Use = use, OnShort = onShort, OnLong = onLong });
            }
            var clashes = new List<Clash>();
            foreach (var trigger in order)
            {
                var list = byTrigger[trigger];
                var shortDoubled = list.Count(heard => heard.OnShort) > 1;
                var longDoubled = list.Count(heard => heard.OnLong) > 1;
                if (!shortDoubled && !longDoubled) continue;
                var doubled = list
                    .Where(heard => (heard.OnShort && shortDoubled) || (heard.OnLong && longDoubled))
                    .Select(heard => heard.Use)
                    .ToList();
                clashes.Add(new Clash(trigger, PanelBindings.TriggerLabel(trigger) ?? trigger, ClashRest(doubled), doubled));
            }
            return clashes;
        }

        /// <summary>One row's use of a trigger, and which of the trigger's two gestures it answers.</summary>
        private sealed class Heard
        {
            public BindingUse Use;
            public bool OnShort;
            public bool OnLong;
        }

        /// <summary>
        /// The line after the trigger's name: what is done, each with the screens it is done on, in the page's
        /// order, then the rig's own, which belong to no screen. One thing done on two screens is said once:
        /// "cycles zone B on both Main dash and Rim, and toggles night mode." Things done on the same screens
        /// are said together before them: "cycles zone A and holds the quick glance on Rim." And a verb the
        /// uses share is said once: "toggles night mode and turns the brightness up and down".
        /// </summary>
        private static string ClashRest(IList<BindingUse> uses)
        {
            var clauses = new List<Clause>();
            var rig = new List<string>();
            foreach (var use in uses)
            {
                var does = use.Does ?? string.Empty;
                if (string.IsNullOrEmpty(use.Place))
                {
                    if (!rig.Contains(does)) rig.Add(does);
                    continue;
                }
                var clause = clauses.FirstOrDefault(one => one.Does.Contains(does));
                if (clause == null)
                {
                    clause = new Clause();
                    clause.Does.Add(does);
                    clauses.Add(clause);
                }
                if (!clause.Places.Contains(use.Place)) clause.Places.Add(use.Place);
            }
            // Then what is done on the very same screens goes into one clause, the first one's.
            var merged = new List<Clause>();
            foreach (var clause in clauses)
            {
                var same = merged.FirstOrDefault(one => one.Places.Count == clause.Places.Count && one.Places.All(clause.Places.Contains));
                if (same == null) merged.Add(clause);
                else same.Does.AddRange(clause.Does);
            }
            var parts = merged
                .Select(clause => Folded(clause.Does) + " on " + (clause.Places.Count == 2 ? "both " : string.Empty) + Listed(clause.Places))
                .ToList();
            if (rig.Count > 0) parts.Add(Folded(rig));
            return Series(parts) + ".";
        }

        /// <summary>What is done, and the screens it is done on, in the page's order.</summary>
        private sealed class Clause
        {
            public readonly List<string> Does = new List<string>();
            public readonly List<string> Places = new List<string>();
        }

        /// <summary>What several rows do, each verb said once, in the order the rows are met: "toggles night
        /// mode and turns the brightness up and down", "cycles zone A and takes zone A and zone D to their
        /// previous pages".</summary>
        private static string Folded(IList<string> does)
        {
            return Listed(does.GroupBy(FirstWord, StringComparer.Ordinal).Select(alike => FoldedAlike(alike.ToList())).ToList());
        }

        private static string FirstWord(string does)
        {
            var space = does.IndexOf(' ');
            return space < 0 ? does : does.Substring(0, space);
        }

        /// <summary>What several rows that start alike do, with the words they all start with said once:
        /// "turns the brightness up and down", "cycles zone A and zone C", and the previous page's tail said
        /// once for every zone it takes back. Listed whole when one would be left with nothing after the
        /// shared words.</summary>
        private static string FoldedAlike(IList<string> does)
        {
            if (does.Count == 1) return does[0];
            if (does.All(one => one.EndsWith(PreviousPageTail, StringComparison.Ordinal)))
            {
                return FoldedAlike(does.Select(one => one.Substring(0, one.Length - PreviousPageTail.Length)).ToList()) + PreviousPagesTail;
            }
            // A zone's name is one word here, in either case, so that "zone A and zone C" is never cut to
            // "zone A and C".
            var words = does.Select(one => Regex.Matches(one, @"(?i:zone|band) \S+|\S+").Cast<Match>().Select(match => match.Value).ToArray()).ToList();
            var shared = 0;
            while (words.All(one => one.Length > shared + 1) && words.All(one => one[shared] == words[0][shared])) shared++;
            if (shared == 0) return Listed(does);
            var prefix = string.Join(" ", words[0].Take(shared));
            return prefix + " " + Listed(words.Select(one => string.Join(" ", one.Skip(shared))).ToList());
        }

        /// <summary>"A and B", or "A on Rim, and B" once a part has an "and" of its own.</summary>
        private static string Series(IList<string> parts)
        {
            if (parts.Count > 1 && parts.Any(part => part.Contains(" and ")))
            {
                return string.Join(", ", parts.Take(parts.Count - 1)) + ", and " + parts[parts.Count - 1];
            }
            return Listed(parts);
        }

        /// <summary>"A and B", "A, B and C".</summary>
        private static string Listed(IList<string> parts)
        {
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        // --- Geometry: Shortcuts.dc.html -----------------------------------------------------------------

        /// <summary>The caption sits 8 under the title (the header's column gap), where every other section
        /// sits PanelShell.SectionGapFor(Shortcuts) under the one before it.</summary>
        public const double IntroGap = 8;

        /// <summary>How far the filter rises above the caption's line, so that the two share a foot as the
        /// header's align-items: flex-end draws them: the 30 px bar less the caption's 19 px line.</summary>
        public const double FilterRaise = 11;

        /// <summary>The filter's gap under the caption when the header stacks.</summary>
        public const double FilterGapStacked = 12;

        // A card's header (.gh): padded 14 by 16 over a rule, the name at 16 SemiBold, 10 to the detail, and the
        // count in the data face at 14.
        public const double HeaderPaddingX = 16;
        public const double HeaderPaddingY = 14;
        public const double GroupTitleSize = 16;
        public const double GroupDetailGap = 10;
        public const double CountSize = 14;

        // A row (.r): padded 10 by 16 under a rule, the name at 14 with its tags 8 after it, the press in a 90
        // px column, the binder after it, 16 between the three.
        public const double RowPaddingX = 16;
        public const double RowPaddingY = 10;
        public const double RowGap = 16;
        public const double RowNameSize = 14;
        public const double TagGap = 8;
        public const double PressWidth = 90;
        public const double CaptionGap = 4;

        /// <summary>SimHub's ControlsEditor is given at least this much wherever it is drawn
        /// (SettingsControl.BuildBinder's MinWidth). This page lowers it: its slot is never narrower beside a
        /// name (<see cref="BinderWidth"/>), and under the name it is the row's whole width, so the floor
        /// could only ever clip SimHub's template at a row narrower than it.</summary>
        public const double BinderMinWidth = 260;

        /// <summary>
        /// The slot a row gives SimHub's editor beside its name. Not the artboard's 260 + 16 + 120: SimHub's
        /// editor is not the artboard's binder. SimHub 9.12.6 (themes/generic.baml) prints each binding on one
        /// line that never wraps, at 10 px: a 58 px press type, then the plugin and the input, each a Label
        /// padded 5 a side, the plugin's 4 from the press type. A long joystick binding is the widest:
        /// "JoystickPlugin" and "Logitech_G923_Racing_Wheel_for_PlayStation_4_and_PC_B05" are 63 and 288 px
        /// of Arial (Liberation Sans' advances), 433 px of line with the press type and the padding, and
        /// the slot's two rules and the template's margins take 10 more. The button's number is the line's
        /// last word, and the only part that says which button is bound. SimHub draws in Segoe UI, which
        /// measures wider than Arial, so the slot keeps some 75 px past that; the page drops the editor's own
        /// name column (SettingsControl.ShortcutsDropNameColumn), so the bindings have all of it.
        /// </summary>
        public const double BinderWidth = 520;

        /// <summary>The name SimHub's editor draws in its own 2* column. None: the row's name, beside it, already
        /// says what it binds, and the page drops the column where SimHub's template has the shape 9.12.6 draws;
        /// where it has another, the column stays and is empty. BuildBinder's fallback text still uses the
        /// binding's BinderName.</summary>
        public const string EditorName = "";

        /// <summary>
        /// The least a row's name is left beside the press and the binder before the binder moves under it:
        /// the widest label a row draws with its New tag on one line. "Band D · previous page" is 136.1 px of
        /// Barlow Regular at 14, and the tag 8 after it is 36.3 ("New" at 10 px SemiBold tracked 0.12, padded
        /// 6 and 5 inside its two rules), 180.4 in all. The glance's caption is not in this column, so the
        /// name needs no more.
        /// </summary>
        public const double NameMinWidth = 184;

        /// <summary>The binder's gap under the name and the press when a row stacks.</summary>
        public const double StackGap = 8;

        // The companion's paging (the artboard's external line): padded 12 by 16, the crumbs 10 from the sentence.
        public const double LeadPaddingX = 16;
        public const double LeadPaddingY = 12;
        public const double LeadGap = 10;

        // The clash line (role=status): padded 12 by 16 on the caution colour at 6 per cent (the artboard's
        // rgba(255,179,0,0.06)), the 16 px icon 12 from the text at 14, and 8 between two lines when two
        // triggers clash.
        public const double BannerTint = 0.06;
        public const double BannerPaddingX = 16;
        public const double BannerPaddingY = 12;
        public const double BannerGap = 12;
        public const double BannerTextSize = 14;
        public const double BannerIconSize = 16;
        public const double BannerStackGap = 8;

        /// <summary>The room inside a row's padding from which it lies flat: the name's floor, the press and
        /// the binder's slot, with the gaps between them.</summary>
        public const double RowFlatFrom = NameMinWidth + RowGap + PressWidth + RowGap + BinderWidth;

        /// <summary>The narrowest content a row lies flat in: the card's two rules and the row's padding
        /// around <see cref="RowFlatFrom"/>.</summary>
        public const double RowStackBelow = CardRules + 2 * RowPaddingX + RowFlatFrom;

        /// <summary>The card's border, left and right.</summary>
        private const double CardRules = 2;

        /// <summary>The room inside a row's padding on a card as wide as the content.</summary>
        public static double RowInside(double contentWidth)
        {
            return Math.Max(0, contentWidth - CardRules - 2 * RowPaddingX);
        }

        /// <summary>
        /// Whether a row puts its binder under its name and press rather than beside them: when the room
        /// inside its padding cannot give the name <see cref="NameMinWidth"/> beside a 90 px press and the
        /// <see cref="BinderWidth"/> slot. Decided as the row is measured, from the room it is given, so the
        /// page reads no width and a resize rebuilds nothing on it.
        /// </summary>
        public static bool RowStacksInside(double inside)
        {
            return inside < RowFlatFrom;
        }

        /// <summary>The same for a card as wide as the content.</summary>
        public static bool RowStacks(double contentWidth)
        {
            return RowStacksInside(RowInside(contentWidth));
        }

        /// <summary>
        /// The width of the binder's slot: <see cref="BinderWidth"/> beside the name, the same in every row so
        /// press and binder line up down a card, and the name column takes the rest; under the name, the
        /// whole of the room inside the row's padding.
        /// </summary>
        public static double BinderSlot(double inside, bool stacks)
        {
            return stacks ? Math.Max(0, inside) : BinderWidth;
        }

        /// <summary>The name's column: what the row has inside its padding past the press, and past the
        /// binder when the binder is beside it.</summary>
        public static double NameColumn(double inside, bool stacks)
        {
            var rest = inside - RowGap - PressWidth;
            if (!stacks) rest -= RowGap + BinderWidth;
            return Math.Max(0, rest);
        }

        /// <summary>A row's caption (the glance's) keeps the measure every row caption in the panel keeps,
        /// as Ui.SettingRow draws it, rather than a paragraph's 620. It runs under the line that holds the
        /// row's name, from the name's left edge across the press and the binder, so it is not wrapped to
        /// the name column's floor.</summary>
        public const double CaptionMaxWidth = 520;

        // --- Search -------------------------------------------------------------------------------------

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(NextPageTitle, PanelPage.Shortcuts, AnchorScreens, "wheel buttons", "bind", "button", "key", "zone"),
            new PanelSearch.Entry(PreviousPageTitle, PanelPage.Shortcuts, AnchorScreens, "back", "zone", "bind"),
            new PanelSearch.Entry(QuickGlanceTitle, PanelPage.Shortcuts, AnchorScreens, "hold", "bind", "button"),
            new PanelSearch.Entry(RigGroupTitle, PanelPage.Shortcuts, AnchorRig, "night", "brightness", "strip", "matrix", "bind"),
            new PanelSearch.Entry(RigActionLabel(Contract.ToggleNightModeAction), PanelPage.Shortcuts, AnchorRig, "night mode button", "bind", "toggle"),
            new PanelSearch.Entry(RigActionLabel(Contract.BrightnessUpAction), PanelPage.Shortcuts, AnchorRig, "brightness buttons", "brighter", "bind"),
            new PanelSearch.Entry(RigActionLabel(Contract.BrightnessDownAction), PanelPage.Shortcuts, AnchorRig, "brightness buttons", "dimmer", "bind"),
            new PanelSearch.Entry(AlertsGroupTitle, PanelPage.Shortcuts, AnchorAlerts, "dismiss", "bind"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = { PanelSoon.RigTest, PanelSoon.AlertDismissal };

        /// <summary>
        /// Search labels this page draws through something other than the constant, each with the text its
        /// sources draw it by, so the list is a record rather than a way round PanelSearchTests.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // A zone's rows read "Band D · next page", built by FaceBindings from these through ZoneRow.
            { NextPageTitle, "PanelShortcuts.FaceBindings(" },
            { PreviousPageTitle, "PanelShortcuts.FaceBindings(" },
            // Every kind's glance row is GlanceBinding's, which labels it QuickGlanceTitle.
            { QuickGlanceTitle, "PanelShortcuts.GlanceBinding(" },
            // The rig's own rows are drawn by their action, through the same function search names them by.
            { RigActionLabel(Contract.ToggleNightModeAction), "PanelShortcuts.LightsBindings(" },
            { RigActionLabel(Contract.BrightnessUpAction), "PanelShortcuts.LightsBindings(" },
            { RigActionLabel(Contract.BrightnessDownAction), "PanelShortcuts.LightsBindings(" },
        };
    }
}
