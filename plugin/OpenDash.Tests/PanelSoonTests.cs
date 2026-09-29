// PanelSoonTests.cs: every greyed control cites an open ticket, and only the tickets this rebuild checked.
//
// The set below was checked against the backlog when #503 was built: every one of these is open. A ticket
// that closes takes its row out of the greyed set (its control is built), so this list changes with it;
// #370, #145 and #487 closed before the rebuild and are deliberately absent, and #322 shipped.
using System;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelSoonTests
    {
        private static readonly int[] Checked =
        {
            381, 96, 383, 112, 321, 114, 319, 320, 152, 116, 85, 146,
            434, 485, 300, 479, 509,
            371, 363, 505,
            506,
            511, 510,
            326, 325, 504, 507, 508, 110, 512, 128, 99, 129, 127, 484, 104,
        };

        [Fact]
        public void Every_ticket_cited_is_one_this_rebuild_checked_and_every_one_checked_is_cited()
        {
            Assert.Equal(Checked.OrderBy(n => n), PanelSoon.Tickets().OrderBy(n => n));
        }

        [Fact]
        public void No_greyed_control_cites_a_closed_ticket()
        {
            foreach (var closed in new[] { 370, 386, 145, 487, 322 }) Assert.DoesNotContain(closed, PanelSoon.Tickets());
        }

        [Fact]
        public void The_hover_names_the_ticket()
        {
            Assert.Equal("Coming soon · #381", PanelSoon.Tip(381));
            Assert.Equal("Coming soon · #146", PanelSoon.Find("Zones instead of cards").Tip);
        }

        [Fact]
        public void The_round_faces_zones_cite_146_and_not_145()
        {
            Assert.Equal(146, PanelSoon.Find("Zones instead of cards").Ticket);
        }

        [Fact]
        public void Each_page_has_its_own_and_a_title_is_listed_once_per_page()
        {
            Assert.Equal(12, PanelSoon.For(PanelPage.Screens).Count());
            Assert.Equal(5, PanelSoon.For(PanelPage.Leds).Count());
            Assert.Equal(3, PanelSoon.For(PanelPage.Matrix).Count());
            Assert.Single(PanelSoon.For(PanelPage.Rig));
            Assert.Equal(2, PanelSoon.For(PanelPage.Shortcuts).Count());
            Assert.Equal(17, PanelSoon.For(PanelPage.Settings).Count());
            foreach (var page in PanelSoon.All.GroupBy(item => item.Page))
            {
                var titles = page.Select(item => item.Title).ToList();
                Assert.Equal(titles.Count, titles.Distinct(StringComparer.Ordinal).Count());
            }
        }

        [Fact]
        public void A_title_is_a_noun_phrase_and_not_an_instruction()
        {
            // voice.md: "Light the real hardware" became "Real hardware", "Light each LED in turn" "Each LED
            // in turn", "Follow the sim's time of day" "Sim time of day".
            Assert.NotNull(PanelSoon.Find("Real hardware"));
            Assert.NotNull(PanelSoon.Find("Each LED in turn"));
            Assert.NotNull(PanelSoon.Find("Sim time of day"));
            foreach (var item in PanelSoon.All)
            {
                Assert.False(item.Title.EndsWith(".", StringComparison.Ordinal), item.Title);
                Assert.DoesNotContain("openDash", item.Title, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void The_tags_are_sentence_case()
        {
            Assert.Equal("Soon", PanelSoon.Tag);
            Assert.Equal("New", PanelSoon.NewTag);
        }
    }
}
