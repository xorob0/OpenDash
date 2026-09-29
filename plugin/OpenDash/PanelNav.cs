// PanelNav.cs: the sidebar's items -- what each page is called, the icon it carries, the count beside it,
// whether it wears the amber dot, and the badge Updates carries at the foot.
//
// The words are sentence case because docs/design/brand.md is taking the uppercase transform away; the
// artboard's capitals are the transform's and not the text's. Pure: PanelNavTests holds it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelNav
    {
        /// <summary>The pages the sidebar lists in its column, in order. Updates is pinned to the foot.</summary>
        public static readonly PanelPage[] Pages =
        {
            PanelPage.Home, PanelPage.Rig, PanelPage.Screens, PanelPage.Leds, PanelPage.Matrix, PanelPage.Shortcuts, PanelPage.Settings,
        };

        public static string Label(PanelPage page)
        {
            switch (page)
            {
                // Each page's own title, so the item and the heading it opens on are one string.
                case PanelPage.Home: return PanelHome.Title;
                case PanelPage.Rig: return PanelRigMap.Title;
                case PanelPage.Screens: return PanelScreens.Title;
                case PanelPage.Leds: return PanelLeds.Title;
                case PanelPage.Matrix: return PanelMatrix.Title;
                case PanelPage.Shortcuts: return PanelShortcuts.Title;
                case PanelPage.Settings: return PanelSettings.Title;
                case PanelPage.Updates: return PanelUpdates.Title;
                default: throw new ArgumentOutOfRangeException("page", page, "no such page");
            }
        }

        /// <summary>The page's sidebar icon, on the twenty unit box.</summary>
        public static string Icon(PanelPage page)
        {
            switch (page)
            {
                case PanelPage.Home: return PanelIcons.Home;
                case PanelPage.Rig: return PanelIcons.Rig;
                case PanelPage.Screens: return PanelIcons.Screens;
                case PanelPage.Leds: return PanelIcons.Leds;
                case PanelPage.Matrix: return PanelIcons.Matrix;
                case PanelPage.Shortcuts: return PanelIcons.Shortcuts;
                case PanelPage.Settings: return PanelIcons.Settings;
                case PanelPage.Updates: return PanelIcons.Updates;
                default: throw new ArgumentOutOfRangeException("page", page, "no such page");
            }
        }

        /// <summary>Whether a rule follows the item: after Rig, which closes the two pages about the whole
        /// rig, and after Matrix, which closes the three about its devices.</summary>
        public static bool GapAfter(PanelPage page)
        {
            return page == PanelPage.Rig || page == PanelPage.Matrix;
        }

        /// <summary>
        /// The number beside an item, or null for none.
        /// </summary>
        /// <remarks>
        /// Screens, LEDs and Matrix count what the rig has, none included: a rig with no matrix reads "0"
        /// beside Matrix, as the ruling on the counts asks. Shortcuts counts the actions somebody has bound,
        /// "0" when the bindings were read and none is bound, and is hidden only when the bindings could not
        /// be read, which is what a null count says; a guess would be a number that is sometimes wrong, and
        /// hiding a zero as well made "nothing bound" look the same as "could not tell".
        /// </remarks>
        public static string Count(PanelPage page, int screens, int strips, int matrices, int? boundShortcuts)
        {
            int? count;
            switch (page)
            {
                case PanelPage.Screens: count = screens; break;
                case PanelPage.Leds: count = strips; break;
                case PanelPage.Matrix: count = matrices; break;
                case PanelPage.Shortcuts: count = boundShortcuts; break;
                default: count = null; break;
            }
            return count.HasValue ? Math.Max(0, count.Value).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        }

        /// <summary>Whether an item wears the amber dot: when Home has something to fix on that page. A fact
        /// the panel could not read produces no issue, so it produces no dot either.</summary>
        public static bool Warns(PanelPage page, IEnumerable<PanelIssue> issues)
        {
            return issues != null && issues.Any(issue => issue != null && issue.Page == page);
        }

        /// <summary>
        /// Whether the Updates item wears the amber dot: when Home has something to fix there that its badge
        /// does not already say. The badge carries a waiting restart and a plugin offer; a strip profile's
        /// update, filed wherever PanelLeds.StripUpdateRoute says its press is, has only the dot, and on
        /// whichever item that page is.
        /// </summary>
        public static bool UpdatesWarns(IEnumerable<PanelIssue> issues)
        {
            return issues != null && issues.Any(issue => issue != null && issue.Page == PanelPage.Updates
                && issue.Id != PanelAttention.UpdateRestart && issue.Id != PanelAttention.UpdateAvailable);
        }

        /// <summary>
        /// What the Updates item carries: "Restart" while an update is waiting for SimHub to close, the
        /// version on offer while there is one, and nothing otherwise.
        /// </summary>
        /// <remarks>
        /// The restart outranks the offer, because since #438 an update stages the plugin and the status
        /// that offered it is still standing afterwards; offering the same version again would ask for a
        /// download that has already happened.
        /// </remarks>
        public static string UpdatesBadge(bool restartPending, bool updateAvailable, string offeredVersion)
        {
            if (restartPending) return RestartBadge;
            if (updateAvailable && !string.IsNullOrWhiteSpace(offeredVersion)) return offeredVersion.Trim();
            return null;
        }

        public const string RestartBadge = "Restart";

        /// <summary>The dot's tooltip on an item that wears it.</summary>
        public const string WarnTooltip = "Needs attention";

        /// <summary>
        /// A rail item's tooltip, which is all the rail says about it: the label, the count, the badge, and
        /// what the amber dot means when the item wears one ("Screens · 5 · Needs attention"). The full
        /// sidebar draws each of these, and its dot carries <see cref="WarnTooltip"/> as its own tooltip.
        /// </summary>
        public static string RailTooltip(PanelPage page, string count, string badge, bool warns)
        {
            var text = Label(page);
            if (count != null) text += " · " + count;
            if (badge != null) text += " · " + badge;
            if (warns) text += " · " + WarnTooltip;
            return text;
        }
    }
}
