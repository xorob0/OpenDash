// PanelSearch.cs: the sidebar's search -- every row and section heading on every page, and how a query finds
// them.
//
// Each page owns its own list (Panel<Page>.Search), because a page agent adding a row is the one who knows
// its words and where it sits, and the shell owns none of them. All() is their concatenation in sidebar
// order, so a hit on Settings never outranks an equally good one on Home. A greyed row is listed as well once
// its page draws it: somebody searching for a theme should find that it is coming rather than conclude it
// does not exist, and should not be sent to a page with no such row.
//
// Pure: PanelSearchTests holds the ranking.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelSearch
    {
        /// <summary>One row the search can find: what it is called, where it is, and other words for it.</summary>
        public sealed class Entry
        {
            public Entry(string label, PanelRoute route, params string[] keywords)
            {
                if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("an entry needs a label", "label");
                Label = label;
                Route = route ?? throw new ArgumentNullException("route");
                Keywords = keywords ?? new string[0];
            }

            public Entry(string label, PanelPage page, string anchor, params string[] keywords)
                : this(label, new PanelRoute(page, anchor), keywords)
            {
            }

            public string Label { get; private set; }

            public PanelRoute Route { get; private set; }

            public string[] Keywords { get; private set; }
        }

        /// <summary>One result: the entry, and how well it matched.</summary>
        public sealed class Hit
        {
            public Hit(Entry entry, int rank)
            {
                Entry = entry;
                Rank = rank;
            }

            public Entry Entry { get; private set; }

            /// <summary>0 for a word of the label starting with the query, 1 for the label containing it, 2
            /// for a keyword. Lower is better.</summary>
            public int Rank { get; private set; }

            public string Label { get { return Entry.Label; } }

            public PanelRoute Route { get { return Entry.Route; } }

            public string PageLabel { get { return PanelNav.Label(Entry.Route.Page); } }

            /// <summary>What the list shows: "Delta reference · Settings", with the separator the rest of the
            /// panel uses, and the label alone when it is its page's own title ("Home", not "Home · Home").</summary>
            public string Text { get { return string.Equals(Label, PageLabel, StringComparison.Ordinal) ? Label : Label + " · " + PageLabel; } }
        }

        public const int DefaultMax = 8;

        /// <summary>What the list says when nothing matched.</summary>
        public const string NoMatch = "No setting matches.";

        public const string Placeholder = "Search";

        /// <summary>The rail's search button, which has no placeholder to say what it does.</summary>
        public const string RailTooltip = "Searches every setting.";

        /// <summary>
        /// The entries a query finds, best first, at most <paramref name="max"/>.
        /// </summary>
        /// <remarks>
        /// A word of the label that starts with the query ranks above the label merely containing it, which
        /// ranks above a keyword: somebody typing "del" wants "Delta reference" before "Model". Within a
        /// rank the entries keep the order the pages list them in. Case is ignored, and so is the space
        /// around the query; an empty query finds nothing, since the list is for answering a question.
        /// </remarks>
        public static IList<Hit> Find(IEnumerable<Entry> entries, string query, int max = DefaultMax)
        {
            var hits = new List<Hit>();
            if (entries == null || max <= 0) return hits;
            var wanted = (query ?? string.Empty).Trim();
            if (wanted.Length == 0) return hits;

            var index = 0;
            var ranked = new List<KeyValuePair<int, Hit>>();
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var rank = Rank(entry, wanted);
                if (rank >= 0) ranked.Add(new KeyValuePair<int, Hit>(index, new Hit(entry, rank)));
                index++;
            }
            return ranked
                .OrderBy(pair => pair.Value.Rank)
                .ThenBy(pair => pair.Key)
                .Take(max)
                .Select(pair => pair.Value)
                .ToList();
        }

        /// <summary>How an entry matches a query, or -1 when it does not.</summary>
        public static int Rank(Entry entry, string query)
        {
            if (entry == null || string.IsNullOrEmpty(query)) return -1;
            if (StartsAWord(entry.Label, query)) return 0;
            if (entry.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            foreach (var keyword in entry.Keywords)
            {
                if (!string.IsNullOrEmpty(keyword) && keyword.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            }
            return -1;
        }

        private static bool StartsAWord(string text, string query)
        {
            var at = 0;
            while (true)
            {
                var found = text.IndexOf(query, at, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return false;
                if (found == 0 || !char.IsLetterOrDigit(text[found - 1])) return true;
                at = found + 1;
            }
        }

        /// <summary>
        /// The entries whose row the rig in <paramref name="settings"/> draws, in All()'s order: what the sidebar
        /// searches. A row only a strip, a matrix or a kind of screen draws is left out of a rig without one, since
        /// its hit would land at the top of a page that has no such row.
        /// </summary>
        public static IEnumerable<Entry> For(OpenDashSettings settings)
        {
            return All().Where(entry => Drawn(entry, settings));
        }

        /// <summary>Whether some page of the rig in <paramref name="settings"/> draws the entry's row: each page
        /// that draws rows only for some of the rig answers for its own (PanelScreens, PanelLeds, PanelMatrix,
        /// PanelShortcuts.SearchDrawn); every other page draws all of its rows on any rig. With no settings to
        /// read, every entry is drawn.</summary>
        public static bool Drawn(Entry entry, OpenDashSettings settings)
        {
            if (entry == null) return false;
            if (settings == null) return true;
            var anchor = entry.Route.Anchor;
            switch (entry.Route.Page)
            {
                case PanelPage.Screens:
                    return PanelScreens.SearchDrawn(anchor, settings.RigScreens());
                case PanelPage.Leds:
                    return PanelLeds.SearchDrawn(anchor, entry.Label, settings.LedBarList());
                case PanelPage.Matrix:
                    return PanelMatrix.SearchDrawn(anchor, settings.MatrixPanels().Count());
                case PanelPage.Shortcuts:
                    return PanelShortcuts.SearchDrawn(anchor, settings.RigScreens());
                default:
                    return true;
            }
        }

        /// <summary>Every page's entries, in sidebar order, then Updates, then every greyed row of the
        /// registry that a page draws, each routed to its page and its own anchor.</summary>
        public static IEnumerable<Entry> All()
        {
            return PanelHome.Search
                .Concat(PanelRigMap.Search)
                .Concat(PanelScreens.Search)
                .Concat(PanelLeds.Search)
                .Concat(PanelMatrix.Search)
                .Concat(PanelShortcuts.Search)
                .Concat(PanelSettings.Search)
                .Concat(PanelUpdates.Search)
                .Concat(Soon);
        }

        /// <summary>
        /// Every greyed row a page says it draws, from each page's own SoonDrawn list in sidebar order. Each
        /// page's list is its own (Panel&lt;Page&gt;.cs), held by PanelSoonTests to that page's sources, so a page
        /// agent that draws a row touches no shell file.
        /// </summary>
        public static IEnumerable<SoonItem> SoonDrawn()
        {
            return PanelHome.SoonDrawn
                .Concat(PanelRigMap.SoonDrawn)
                .Concat(PanelScreens.SoonDrawn)
                .Concat(PanelLeds.SoonDrawn)
                .Concat(PanelMatrix.SoonDrawn)
                .Concat(PanelShortcuts.SoonDrawn)
                .Concat(PanelSettings.SoonDrawn)
                .Concat(PanelUpdates.SoonDrawn);
        }

        /// <summary>Whether search may send a driver to a greyed row: a page draws it, and not only inside a
        /// sheet, which search cannot open.</summary>
        public static bool Lists(SoonItem item)
        {
            return item != null && !item.InSheetOnly && SoonDrawn().Contains(item);
        }

        /// <summary>The greyed rows the pages draw outside a sheet, as entries: found by their title, and by
        /// "soon". A row no page draws yet is not listed, since its hit would land on nothing.</summary>
        public static readonly Entry[] Soon = SoonDrawn()
            .Where(item => !item.InSheetOnly)
            .Distinct()
            .Select(item => new Entry(item.Title, item.Page, item.Anchor, PanelSoon.Tag.ToLowerInvariant(), "coming"))
            .ToArray();
    }
}
