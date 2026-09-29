// PanelSoonTests.cs: every greyed control cites an open ticket, and only the tickets this rebuild checked.
//
// The set below was checked against the backlog when #503 was built: every one of these is open. A ticket
// that closes takes its row out of the greyed set (its control is built), so this list changes with it;
// #370, #145 and #487 closed before the rebuild and are deliberately absent, and #322 shipped.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            // in turn", "Follow the sim's time of day" "Sim time of day"; and the review's three, "Run the
            // Rig test", "Dismiss the alert" and "Colour everything by RPM".
            Assert.NotNull(PanelSoon.Find("Real hardware"));
            Assert.NotNull(PanelSoon.Find("Each LED in turn"));
            Assert.NotNull(PanelSoon.Find("Sim time of day"));
            Assert.NotNull(PanelSoon.Find("Rig test"));
            Assert.NotNull(PanelSoon.Find("Alert dismissal"));
            Assert.NotNull(PanelSoon.Find("RPM colour for everything"));
            // No title opens on a verb telling the driver to do something.
            // Verbs only: "Colour vision" and "Colours" are nouns, so "Colour" is not listed; the old "Colour
            // everything by RPM" is held out by name.
            Assert.Null(PanelSoon.Find("Colour everything by RPM"));
            var imperatives = new[] { "Run", "Dismiss", "Follow", "Dim", "Use", "Show", "Sweep", "Try", "Set", "Add", "Turn", "Pick", "Choose", "Enable", "Disable" };
            // Nor on a question word: "Where each alert shows" is a sentence pretending to be a heading, the
            // failure voice.md names in "What each zone shows". #512's row is "Alert display", in the panel's
            // own "X display" pattern.
            var clauses = new[] { "Where", "What", "When", "Which", "How", "Who", "Why", "Whether" };
            Assert.NotNull(PanelSoon.Find("Alert display"));
            Assert.Null(PanelSoon.Find("Where each alert shows"));
            foreach (var item in PanelSoon.All)
            {
                var first = item.Title.Split(' ')[0];
                Assert.DoesNotContain(first, imperatives);
                Assert.DoesNotContain(first, clauses);
                Assert.False(item.Title.EndsWith(".", StringComparison.Ordinal), item.Title);
                Assert.DoesNotContain("openDash", item.Title, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Every_greyed_row_has_an_anchor_of_its_own()
        {
            Assert.Equal("soon.506.realhardware", PanelSoon.Find("Real hardware").Anchor);
            Assert.Equal(PanelSoon.All.Count, PanelSoon.All.Select(item => item.Anchor).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void The_tags_are_sentence_case()
        {
            Assert.Equal("Soon", PanelSoon.Tag);
            Assert.Equal("New", PanelSoon.NewTag);
        }

        /// <summary>
        /// The rows search may send a driver to are the rows the pages draw: a page that draws every row the
        /// registry gives it calls PanelSoon.For(PanelPage.X), and a page that draws one calls
        /// PanelSoon.Find(title). Read from the sources, because the pages are WPF and no test compiles them;
        /// a page agent that draws its rows has to list its page in DrawnOnPages for search to find them.
        /// </summary>
        [Fact]
        public void Search_lists_exactly_the_greyed_rows_the_pages_draw()
        {
            var pages = new HashSet<PanelPage>();
            var titles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in RepoPaths.SettingsControlSources())
            {
                var text = File.ReadAllText(source);
                foreach (Match call in Regex.Matches(text, @"PanelSoon\.For\(PanelPage\.(\w+)\)"))
                {
                    pages.Add((PanelPage)Enum.Parse(typeof(PanelPage), call.Groups[1].Value));
                }
                foreach (Match call in Regex.Matches(text, @"PanelSoon\.Find\(([^)]+)\)"))
                {
                    titles.Add(Resolve(call.Groups[1].Value.Trim()));
                }
            }
            Assert.Equal(pages.OrderBy(p => p), PanelSoon.DrawnOnPages.OrderBy(p => p));
            Assert.Equal(titles.OrderBy(t => t, StringComparer.Ordinal), PanelSoon.DrawnOneByOne.OrderBy(t => t, StringComparer.Ordinal));
        }

        /// <summary>A Find argument as the title it names: a literal, or a Panel constant read by reflection.</summary>
        private static string Resolve(string argument)
        {
            if (argument.StartsWith("\"", StringComparison.Ordinal)) return argument.Trim('"');
            var dot = argument.LastIndexOf('.');
            Assert.True(dot > 0, "PanelSoon.Find is given " + argument + ", which this test cannot read");
            var type = typeof(PanelSoon).Assembly.GetType("OpenDashPlugin." + argument.Substring(0, dot));
            Assert.NotNull(type);
            var field = type.GetField(argument.Substring(dot + 1));
            Assert.NotNull(field);
            return (string)field.GetValue(null);
        }
    }
}
