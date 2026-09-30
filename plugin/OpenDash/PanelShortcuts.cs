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

        /// <summary>A zone's row, and the name its binder gives the action in SimHub's own list, in the
        /// same words: "Band D · next page", "Rim · Band D · previous page".</summary>
        public static string ZoneRow(string zoneLabel, bool next)
        {
            return zoneLabel + Join + (next ? NextPageTitle : PreviousPageTitle).ToLowerInvariant();
        }

        public static string ZoneBinderName(string screenName, string zoneLabel, bool next)
        {
            return screenName + Join + ZoneRow(zoneLabel, next);
        }

        /// <summary>The glance's name in SimHub's own list: "Rim · quick glance".</summary>
        public static string GlanceBinderName(string screenName)
        {
            return screenName + Join + QuickGlanceTitle.ToLowerInvariant();
        }

        private const string Join = " · ";

        // --- Rows ---------------------------------------------------------------------------------------

        /// <summary>
        /// One row of a card: the action it binds, its label, the name SimHub's list gives the binding, the
        /// press it answers to, whether it carries the New tag, and what it does in the clash line's words.
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
        /// A face's zone rows, in the order Shortcuts.dc.html lists them: every zone's next page in the
        /// picture's order, then every zone's previous page (new in this release). The glance row, which is
        /// the card's last, is <see cref="GlanceBinding"/>, because its binder is the one that holds.
        /// </summary>
        /// <param name="zoneOrder">PanelFacePlan.ZoneOrder(face), or Contract.FaceZoneLetters for a face
        /// whose size OpenDash does not know, so that no action goes without a row.</param>
        public static IList<Binding> FaceBindings(string ns, string screenName, IList<string> zoneOrder)
        {
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

        /// <summary>The quick glance's row, the same on a face, a pit wall and a companion: held.</summary>
        public static Binding GlanceBinding(string ns, string screenName)
        {
            return new Binding(Contract.HoldQuickGlanceActionFor(ns), QuickGlanceTitle, GlanceBinderName(screenName), true, false, GlanceDoes);
        }

        /// <summary>The Lights card's live rows: night mode, and brightness up and down, which are new.</summary>
        public static IList<Binding> LightsBindings()
        {
            return Contract.RigActionNames()
                .Select(action => new Binding(action, RigActionLabel(action), RigActionLabel(action), false, action != Contract.ToggleNightModeAction, RigActionDoes(action)))
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
        /// ("Face · 1280 × 480", "Pit wall · 1920 × 1080"). The kind alone when the size is not known.
        /// </summary>
        public static string GroupDetail(string kind, int width, int height)
        {
            var name = PanelAddScreen.KindName(kind);
            if (width <= 0 || height <= 0) return name;
            return name + Join + width.ToString(CultureInfo.InvariantCulture) + " × " + height.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A card's count, "3 of 6": how many of its rows are bound. Null for a card with nothing to
        /// bind, whose greyed rows are not counted, since no press could ever complete the count.</summary>
        public static string Count(int bound, int total)
        {
            if (total <= 0) return null;
            return bound.ToString(CultureInfo.InvariantCulture) + " of " + total.ToString(CultureInfo.InvariantCulture);
        }

        // --- The companion's paging ---------------------------------------------------------------------

        public const string ControlsAndEventsCrumb = "Controls and events";
        public const string NextScreenCrumb = "NextScreen";

        /// <summary>Where SimHub binds a companion's paging, beside PanelCopy.CompanionPaging: Devices, the
        /// device the companion runs on (its screen's name, which is what a driver called it), Controls and
        /// events, NextScreen.</summary>
        public static string[] PagingCrumbs(string deviceName)
        {
            return new[] { PanelAttention.DevicesCrumb, deviceName, ControlsAndEventsCrumb, NextScreenCrumb };
        }

        // --- The filter ---------------------------------------------------------------------------------

        public const string FilterAll = "all";
        public const string FilterBound = "bound";
        public const string FilterNotBound = "not-bound";

        public static readonly string[] FilterValues = { FilterAll, FilterBound, FilterNotBound };
        public static readonly string[] FilterLabels = { "All", "Bound", PanelBindings.NotBound };

        /// <summary>
        /// Whether a row shows under the filter. A greyed row cannot be bound, so it is neither a binding
        /// nor one still to make, and shows under All alone.
        /// </summary>
        public static bool Shows(string filter, bool bindable, bool bound)
        {
            if (filter == FilterBound) return bindable && bound;
            if (filter == FilterNotBound) return bindable && !bound;
            return true;
        }

        /// <summary>
        /// The filter a build draws: the one chosen earlier in the session, unless the page was opened on a
        /// row (a binding chip's press, a search hit), which the filter must not hide, or SimHub's mappings
        /// cannot be read, when there is no filter at all.
        /// </summary>
        public static string FilterFor(string chosen, string anchor, bool readable)
        {
            if (!readable || !string.IsNullOrEmpty(anchor)) return FilterAll;
            return Array.IndexOf(FilterValues, chosen) >= 0 ? chosen : FilterAll;
        }

        /// <summary>What the page says when the filter leaves no row: null under All, which always has one.</summary>
        public static string FilterEmpty(string filter)
        {
            if (filter == FilterBound) return "Nothing is bound yet.";
            if (filter == FilterNotBound) return "Every shortcut is bound.";
            return null;
        }

        // --- The clash line -----------------------------------------------------------------------------

        /// <summary>One binding of one trigger: the raw trigger SimHub stores, the card it is on (a screen's
        /// name, or null for the rig's lights) and what the row does, in the clash line's words.</summary>
        public sealed class BindingUse
        {
            public BindingUse(string trigger, string place, string does)
            {
                Trigger = trigger;
                Place = place;
                Does = does;
            }

            public string Trigger { get; private set; }
            public string Place { get; private set; }
            public string Does { get; private set; }
        }

        /// <summary>A trigger bound to more than one row: its name, drawn strong, and the rest of the line.</summary>
        public sealed class Clash
        {
            public Clash(string trigger, string lead, string rest)
            {
                Trigger = trigger;
                Lead = lead;
                Rest = rest;
            }

            public string Trigger { get; private set; }
            public string Lead { get; private set; }
            public string Rest { get; private set; }
            public string Text { get { return Lead + " " + Rest; } }
        }

        /// <summary>What a zone's row does, in the clash line: "cycles zone B", "cycles band D back".</summary>
        public static string ZoneDoes(string zoneLabel, bool next)
        {
            var zone = string.IsNullOrEmpty(zoneLabel) ? zoneLabel : char.ToLowerInvariant(zoneLabel[0]) + zoneLabel.Substring(1);
            return "cycles " + zone + (next ? string.Empty : " back");
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
        /// action is one use.
        /// </remarks>
        public static IList<Clash> Clashes(IEnumerable<BindingUse> uses)
        {
            var order = new List<string>();
            var byTrigger = new Dictionary<string, List<BindingUse>>(StringComparer.Ordinal);
            foreach (var use in uses ?? Enumerable.Empty<BindingUse>())
            {
                if (use == null || string.IsNullOrWhiteSpace(use.Trigger)) continue;
                List<BindingUse> list;
                if (!byTrigger.TryGetValue(use.Trigger, out list))
                {
                    list = new List<BindingUse>();
                    byTrigger.Add(use.Trigger, list);
                    order.Add(use.Trigger);
                }
                if (list.Any(seen => seen.Place == use.Place && seen.Does == use.Does)) continue;
                list.Add(use);
            }
            var clashes = new List<Clash>();
            foreach (var trigger in order)
            {
                var list = byTrigger[trigger];
                if (list.Count < 2) continue;
                clashes.Add(new Clash(trigger, PanelBindings.TriggerLabel(trigger) ?? trigger, ClashRest(list)));
            }
            return clashes;
        }

        private static string ClashRest(IList<BindingUse> uses)
        {
            var sameDoes = uses.All(use => use.Does == uses[0].Does);
            var everyPlaced = uses.All(use => !string.IsNullOrEmpty(use.Place));
            if (sameDoes && everyPlaced)
            {
                var places = uses.Select(use => use.Place).ToList();
                return uses[0].Does + " on " + (places.Count == 2 ? "both " : string.Empty) + Listed(places) + ".";
            }
            var samePlace = everyPlaced && uses.All(use => use.Place == uses[0].Place);
            if (samePlace) return Listed(uses.Select(use => use.Does).ToList()) + " on " + uses[0].Place + ".";
            return Listed(uses.Select(use => string.IsNullOrEmpty(use.Place) ? use.Does : use.Does + " on " + use.Place).ToList()) + ".";
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

        /// <summary>SimHub's ControlsEditor is given at least this much (SettingsControl.BuildBinder).</summary>
        public const double BinderMinWidth = 260;

        /// <summary>The least a row's name is left beside the press and the binder before the binder moves
        /// under it.</summary>
        public const double NameMinWidth = 160;

        /// <summary>The binder's gap under the name and the press when a row stacks.</summary>
        public const double StackGap = 8;

        // The companion's paging (the artboard's external line): padded 12 by 16, the crumbs 10 from the sentence.
        public const double LeadPaddingX = 16;
        public const double LeadPaddingY = 12;
        public const double LeadGap = 10;

        // The clash line (role=status): padded 12 by 16, the 16 px icon 12 from the text at 14, and 8 between
        // two lines when two triggers clash.
        public const double BannerPaddingX = 16;
        public const double BannerPaddingY = 12;
        public const double BannerGap = 12;
        public const double BannerTextSize = 14;
        public const double BannerIconSize = 16;
        public const double BannerStackGap = 8;

        /// <summary>
        /// Whether a row puts its binder under its name and press rather than beside them: when a card as wide
        /// as the content cannot give the name <see cref="NameMinWidth"/> beside a 90 px press and a 260 px
        /// binder. A row's width, not the sidebar's: TwoColumns is about two blocks of a page.
        /// </summary>
        public static bool RowStacks(double contentWidth)
        {
            return contentWidth < RowStackBelow;
        }

        /// <summary>The narrowest content a row lies flat in: the card's two rules, the row's padding, the name,
        /// the press and the binder with the gaps between them.</summary>
        public const double RowStackBelow = 2 + 2 * RowPaddingX + NameMinWidth + RowGap + PressWidth + RowGap + BinderMinWidth;

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
