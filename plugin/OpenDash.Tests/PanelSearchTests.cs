// PanelSearchTests.cs: how the sidebar's search ranks what it finds, and that every page's rows are in it.
using System;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelSearchTests
    {
        private static readonly PanelSearch.Entry[] Entries =
        {
            new PanelSearch.Entry("Model year", PanelPage.Home, null),
            new PanelSearch.Entry("Delta reference", PanelPage.Settings, "settings.race-data", "lap"),
            new PanelSearch.Entry("Night mode", PanelPage.Settings, "settings.lighting", "dark"),
            new PanelSearch.Entry("Idle display", PanelPage.Matrix, null, "at rest", "gear"),
            new PanelSearch.Entry("Redline flash", PanelPage.Matrix, null, "gear"),
        };

        [Fact]
        public void A_word_that_starts_with_the_query_outranks_a_label_that_contains_it()
        {
            var hits = PanelSearch.Find(Entries, "del");
            // "Delta reference" starts a word with it; "Model year" only contains it.
            Assert.Equal(new[] { "Delta reference", "Model year" }, hits.Select(h => h.Label));
            Assert.Equal(new[] { 0, 1 }, hits.Select(h => h.Rank));
        }

        [Fact]
        public void A_keyword_ranks_last_and_the_pages_order_holds_within_a_rank()
        {
            var hits = PanelSearch.Find(Entries, "gear");
            Assert.Equal(new[] { "Idle display", "Redline flash" }, hits.Select(h => h.Label));
            Assert.All(hits, hit => Assert.Equal(2, hit.Rank));
        }

        [Fact]
        public void A_later_word_of_the_label_counts_as_a_start()
        {
            Assert.Equal(0, PanelSearch.Rank(Entries[2], "mode"));
            Assert.Equal(0, PanelSearch.Rank(Entries[1], "REF"));
        }

        [Fact]
        public void Case_and_the_space_around_a_query_are_ignored_and_an_empty_query_finds_nothing()
        {
            Assert.Single(PanelSearch.Find(Entries, "  NIGHT "));
            Assert.Empty(PanelSearch.Find(Entries, ""));
            Assert.Empty(PanelSearch.Find(Entries, "   "));
            Assert.Empty(PanelSearch.Find(Entries, null));
            Assert.Empty(PanelSearch.Find(Entries, "zzz"));
        }

        [Fact]
        public void At_most_eight_are_listed()
        {
            var many = Enumerable.Range(0, 20).Select(i => new PanelSearch.Entry("Row " + i, PanelPage.Home, null)).ToArray();
            Assert.Equal(8, PanelSearch.Find(many, "row").Count);
            Assert.Equal(3, PanelSearch.Find(many, "row", 3).Count);
        }

        [Fact]
        public void A_hit_reads_as_its_label_and_its_page_and_leads_to_its_row()
        {
            var hit = PanelSearch.Find(Entries, "night").Single();
            Assert.Equal("Night mode — Settings", hit.Text);
            Assert.Equal(PanelRoute.To(PanelPage.Settings, "settings.lighting"), hit.Route);
            Assert.Equal("No setting matches.", PanelSearch.NoMatch);
        }

        [Fact]
        public void Every_page_contributes_and_every_entry_names_its_own_page()
        {
            var all = PanelSearch.All().ToList();
            foreach (var page in Enum.GetValues(typeof(PanelPage)).Cast<PanelPage>())
            {
                Assert.Contains(all, entry => entry.Route.Page == page);
            }
            Assert.All(PanelHome.Search, e => Assert.Equal(PanelPage.Home, e.Route.Page));
            Assert.All(PanelRigMap.Search, e => Assert.Equal(PanelPage.Rig, e.Route.Page));
            Assert.All(PanelScreens.Search, e => Assert.Equal(PanelPage.Screens, e.Route.Page));
            Assert.All(PanelLeds.Search, e => Assert.Equal(PanelPage.Leds, e.Route.Page));
            Assert.All(PanelMatrix.Search, e => Assert.Equal(PanelPage.Matrix, e.Route.Page));
            Assert.All(PanelShortcuts.Search, e => Assert.Equal(PanelPage.Shortcuts, e.Route.Page));
            Assert.All(PanelSettings.Search, e => Assert.Equal(PanelPage.Settings, e.Route.Page));
            Assert.All(PanelUpdates.Search, e => Assert.Equal(PanelPage.Updates, e.Route.Page));
        }

        [Fact]
        public void The_settings_that_moved_off_the_old_tabs_are_found_where_they_went()
        {
            Assert.Equal(PanelPage.Settings, PanelSearch.Find(PanelSearch.All(), "delta precision").First().Route.Page);
            Assert.Equal(PanelPage.Settings, PanelSearch.Find(PanelSearch.All(), "flags in the pit lane").First().Route.Page);
            Assert.Equal(PanelPage.Leds, PanelSearch.Find(PanelSearch.All(), "centre display").First().Route.Page);
            Assert.Equal(PanelPage.Updates, PanelSearch.Find(PanelSearch.All(), "put mine back").First().Route.Page);
        }

        [Fact]
        public void An_entry_needs_a_label_and_a_route()
        {
            Assert.Throws<ArgumentException>(() => new PanelSearch.Entry(" ", PanelRoute.Home));
            Assert.Throws<ArgumentNullException>(() => new PanelSearch.Entry("Row", (PanelRoute)null));
        }
    }
}
