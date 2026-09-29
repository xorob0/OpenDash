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
            Assert.Equal("soon.506.realhardware", PanelSoon.RealHardware.Anchor);
            Assert.Equal(PanelSoon.All.Count, PanelSoon.All.Select(item => item.Anchor).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void The_tags_are_sentence_case()
        {
            Assert.Equal("Soon", PanelSoon.Tag);
            Assert.Equal("New", PanelSoon.NewTag);
        }

        /// <summary>
        /// The ruled rows, as (title, ticket, page): the ticket set and each page's count alone let two rows
        /// swap tickets and stay green -- Pit limiter lights 509 with Priority order 505, Rig test 511 with
        /// Alert dismissal 510, Yellow flags 504 with Incidents 508.
        /// </summary>
        [Theory]
        [InlineData("Zones instead of cards", 146, PanelPage.Screens)]
        [InlineData("Yellow flags", 504, PanelPage.Settings)]
        [InlineData("Priority order", 505, PanelPage.Matrix)]
        [InlineData("Real hardware", 506, PanelPage.Rig)]
        [InlineData("Tyre wear", 507, PanelPage.Settings)]
        [InlineData("Pit window open", 507, PanelPage.Settings)]
        [InlineData("Incidents", 508, PanelPage.Settings)]
        [InlineData("Pit limiter lights", 509, PanelPage.Leds)]
        [InlineData("Alert dismissal", 510, PanelPage.Shortcuts)]
        [InlineData("Rig test", 511, PanelPage.Shortcuts)]
        [InlineData("Alert display", 512, PanelPage.Settings)]
        [InlineData("Sim time of day", 128, PanelPage.Settings)]
        [InlineData("Screen dimming", 128, PanelPage.Settings)]
        public void A_ruled_row_cites_its_own_ticket_on_its_own_page(string title, int ticket, PanelPage page)
        {
            var item = PanelSoon.Find(title);
            Assert.NotNull(item);
            Assert.Equal(ticket, item.Ticket);
            Assert.Equal(page, item.Page);
        }

        /// <summary>
        /// Only the registry greys a row. SoonItem's constructor is internal, and no plugin file but
        /// PanelSoon.cs constructs one or writes the "Coming soon" hover, so a closed ticket such as #370 or
        /// #322 cannot be greyed from a page with a number typed beside it and pass every test here.
        /// </summary>
        [Fact]
        public void Only_the_registry_greys_a_row()
        {
            var offenders = new List<string>();
            foreach (var path in Directory.GetFiles(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash"), "*.cs"))
            {
                if (Path.GetFileName(path) == "PanelSoon.cs") continue;
                var code = RepoPaths.Code(path);
                if (code.Contains("new SoonItem(")) offenders.Add(Path.GetFileName(path) + ": new SoonItem(");
                if (code.Contains("Coming soon")) offenders.Add(Path.GetFileName(path) + ": \"Coming soon\"");
            }
            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
            Assert.All(typeof(SoonItem).GetConstructors(), ctor => Assert.False(ctor.IsPublic, "SoonItem's constructor is public"));
        }

        /// <summary>Every registry entry is a named field, so a page draws Ui.Soon(row, PanelSoon.RevFill)
        /// rather than finding one by a title typed again, and All is exactly those fields.</summary>
        [Fact]
        public void Every_entry_is_named()
        {
            var named = typeof(PanelSoon).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.FieldType == typeof(SoonItem))
                .Select(field => (SoonItem)field.GetValue(null))
                .ToList();
            Assert.Equal(PanelSoon.All.Count, named.Count);
            Assert.All(PanelSoon.All, item => Assert.Contains(item, named));
        }

        /// <summary>The sheet's two tiles are drawn only inside the Add screen sheet, which search cannot
        /// open, and are the only such rows.</summary>
        [Fact]
        public void Only_the_add_sheets_tiles_are_drawn_in_a_sheet_alone()
        {
            Assert.Equal(new[] { 116, 85 }, PanelSoon.All.Where(item => item.InSheetOnly).Select(item => item.Ticket));
        }

        /// <summary>The page model and the files that build each page, which its SoonDrawn is held to.</summary>
        private static readonly Dictionary<PanelPage, (Type Model, string Sources)> Pages = new Dictionary<PanelPage, (Type, string)>
        {
            { PanelPage.Home, (typeof(PanelHome), "SettingsControl.Home") },
            { PanelPage.Rig, (typeof(PanelRigMap), "SettingsControl.Rig") },
            { PanelPage.Screens, (typeof(PanelScreens), "SettingsControl.Screens") },
            { PanelPage.Leds, (typeof(PanelLeds), "SettingsControl.Lights") },
            { PanelPage.Matrix, (typeof(PanelMatrix), "SettingsControl.Matrix") },
            { PanelPage.Shortcuts, (typeof(PanelShortcuts), "SettingsControl.Shortcuts") },
            { PanelPage.Settings, (typeof(PanelSettings), "SettingsControl.Settings") },
            { PanelPage.Updates, (typeof(PanelUpdates), "SettingsControl.Updates") },
        };

        /// <summary>
        /// Each page's SoonDrawn, in its own Panel&lt;Page&gt;.cs, is exactly the greyed rows that page's own
        /// sources draw: by name (PanelSoon.RevFill), by PanelSoon.Find(title), or all of a page's entries
        /// through PanelSoon.For(PanelPage.X). Read from the sources, because the pages are WPF and no test
        /// compiles them. A page agent that draws a row adds it to its own list, and no shell file moves.
        /// </summary>
        [Theory]
        [InlineData(PanelPage.Home)]
        [InlineData(PanelPage.Rig)]
        [InlineData(PanelPage.Screens)]
        [InlineData(PanelPage.Leds)]
        [InlineData(PanelPage.Matrix)]
        [InlineData(PanelPage.Shortcuts)]
        [InlineData(PanelPage.Settings)]
        [InlineData(PanelPage.Updates)]
        public void A_page_lists_exactly_the_greyed_rows_its_own_sources_draw(PanelPage page)
        {
            var (model, prefix) = Pages[page];
            var code = string.Join("\n", Directory.GetFiles(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash"), prefix + "*.cs")
                .Where(path => Path.GetFileName(path) == prefix + ".cs" || Path.GetFileName(path).StartsWith(prefix + ".", StringComparison.Ordinal))
                .Select(RepoPaths.Code));
            var drawn = new HashSet<SoonItem>();
            foreach (Match call in Regex.Matches(code, @"PanelSoon\.For\(PanelPage\.(\w+)\)"))
            {
                foreach (var item in PanelSoon.For((PanelPage)Enum.Parse(typeof(PanelPage), call.Groups[1].Value))) drawn.Add(item);
            }
            foreach (Match call in Regex.Matches(code, @"PanelSoon\.Find\(([^)]+)\)"))
            {
                var item = PanelSoon.Find(Resolve(call.Groups[1].Value.Trim()));
                Assert.NotNull(item);
                drawn.Add(item);
            }
            foreach (Match name in Regex.Matches(code, @"PanelSoon\.(\w+)"))
            {
                var field = typeof(PanelSoon).GetField(name.Groups[1].Value);
                if (field != null && field.FieldType == typeof(SoonItem)) drawn.Add((SoonItem)field.GetValue(null));
            }
            var listed = (SoonItem[])model.GetField("SoonDrawn").GetValue(null);
            Assert.Equal(drawn.Select(item => item.Anchor).OrderBy(a => a, StringComparer.Ordinal), listed.Select(item => item.Anchor).OrderBy(a => a, StringComparer.Ordinal));
            Assert.All(listed, item => Assert.Equal(page, item.Page));
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
