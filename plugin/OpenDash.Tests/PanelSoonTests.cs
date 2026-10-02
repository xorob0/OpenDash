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
            var imperatives = new[] { "Run", "Dismiss", "Follow", "Dim", "Use", "Show", "Sweep", "Try", "Set", "Add", "Turn", "Pick", "Choose", "Enable", "Disable", "Light", "Drag", "Flash" };
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
        /// Every greyed row, as (title, ticket, page, drawn only in a sheet), in the order All lists them. The
        /// ticket set and each page's count alone let two rows swap tickets and stay green -- Fit 319 with
        /// Circle tracker 320, Fuel target per lap 326 with Tyre display 325 -- and let a title be reworded
        /// with no test noticing, so each row is held here whole: a change to one is a change to this table.
        /// </summary>
        private static readonly (string Title, int Ticket, PanelPage Page, bool InSheetOnly)[] Registry =
        {
            ("Rev fill under the lights", 381, PanelPage.Screens, false),
            ("Spotter at the rev bar ends", 96, PanelPage.Screens, false),
            ("Pit page in the pit lane", 383, PanelPage.Screens, false),
            ("Pop-ups", 112, PanelPage.Screens, false),
            ("Edge lights for the delta", 321, PanelPage.Screens, false),
            ("Screen care", 114, PanelPage.Screens, false),
            ("Fit", 319, PanelPage.Screens, false),
            ("Circle tracker", 320, PanelPage.Screens, false),
            ("Launch", 152, PanelPage.Screens, false),
            ("Flags screen", 116, PanelPage.Screens, true),
            ("Your displays", 85, PanelPage.Screens, true),
            ("Zones instead of cards", 146, PanelPage.Screens, false),
            ("Each LED in turn", 434, PanelPage.Leds, false),
            ("Idle sweep", 485, PanelPage.Leds, false),
            ("Engine start animation", 300, PanelPage.Leds, false),
            ("Car data for AC, ACC and LMU", 479, PanelPage.Leds, false),
            ("Pit limiter lights", 509, PanelPage.Leds, false),
            ("RPM colour for everything", 371, PanelPage.Matrix, false),
            ("SimHub device", 363, PanelPage.Matrix, false),
            ("Priority order", 505, PanelPage.Matrix, false),
            ("Real hardware", 506, PanelPage.Rig, false),
            ("Rig test", 511, PanelPage.Shortcuts, false),
            ("Alert dismissal", 510, PanelPage.Shortcuts, false),
            ("Fuel target per lap", 326, PanelPage.Settings, false),
            ("Tyre display", 325, PanelPage.Settings, false),
            ("Yellow flags", 504, PanelPage.Settings, false),
            ("Tyre wear", 507, PanelPage.Settings, false),
            ("Pit window open", 507, PanelPage.Settings, false),
            ("Incidents", 508, PanelPage.Settings, false),
            ("Hybrid battery low", 110, PanelPage.Settings, false),
            ("Alert display", 512, PanelPage.Settings, false),
            ("Sim time of day", 128, PanelPage.Settings, false),
            ("Screen dimming", 128, PanelPage.Settings, false),
            ("Theme", 99, PanelPage.Settings, false),
            ("Colour vision", 129, PanelPage.Settings, false),
            ("Colours", 127, PanelPage.Settings, false),
            ("Name", 484, PanelPage.Settings, false),
            ("Race number", 484, PanelPage.Settings, false),
            ("Logo", 484, PanelPage.Settings, false),
            ("Idle screen background", 104, PanelPage.Settings, false),
        };

        [Fact]
        public void The_registry_is_exactly_these_rows_in_order()
        {
            Assert.Equal(40, Registry.Length);
            Assert.Equal(
                Registry.Select(row => row.Title + " #" + row.Ticket + " " + row.Page + (row.InSheetOnly ? " sheet" : "")),
                PanelSoon.All.Select(item => item.Title + " #" + item.Ticket + " " + item.Page + (item.InSheetOnly ? " sheet" : "")));
        }

        /// <summary>
        /// Only the registry greys a row. SoonItem's constructor is internal, and no plugin file but
        /// PanelSoon.cs constructs one or writes the "Coming soon" hover; nor does any file but PanelSoon.cs
        /// and the kit that draws an entry call PanelSoon.Tip(ticket), which takes a bare number, or draw the
        /// kit's bare Soon tag, which Ui.SoonTag(item) draws only for an entry. So a closed ticket such as
        /// #370 or #322 cannot be greyed from a page with a number typed beside it and pass every test here.
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
                if (Path.GetFileName(path) == "Widgets.Kit.cs") continue;
                if (code.Contains("PanelSoon.Tip(")) offenders.Add(Path.GetFileName(path) + ": PanelSoon.Tip(");
                if (Regex.IsMatch(code, @"\bSoonTag\(\s*\)")) offenders.Add(Path.GetFileName(path) + ": SoonTag()");
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

        /// <summary>
        /// A greyed row belongs to a page, which lists it in its SoonDrawn and search lists from there, so no
        /// shell partial draws one: a row drawn from SettingsControl.Profiles.cs or .Sheet.cs would be in no
        /// page's list, search would never find it, and the per-page test above reads page files only.
        /// </summary>
        [Fact]
        public void No_shell_file_draws_a_greyed_row()
        {
            var entries = typeof(PanelSoon).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.FieldType == typeof(SoonItem))
                .Select(field => field.Name)
                .ToList();
            var offenders = new List<string>();
            foreach (var path in RepoPaths.SettingsControlSources())
            {
                var file = Path.GetFileName(path);
                if (Pages.Values.Any(p => file == p.Sources + ".cs" || file.StartsWith(p.Sources + ".", StringComparison.Ordinal))) continue;
                var code = RepoPaths.Code(path);
                if (code.Contains("PanelSoon.For(")) offenders.Add(file + ": PanelSoon.For(");
                if (code.Contains("PanelSoon.Find(")) offenders.Add(file + ": PanelSoon.Find(");
                foreach (var entry in entries)
                {
                    if (Regex.IsMatch(code, @"\bPanelSoon\." + entry + @"\b")) offenders.Add(file + ": PanelSoon." + entry);
                }
            }
            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
            // The guard reads the shell partials, and there are shell partials to read.
            Assert.Contains(RepoPaths.SettingsControlSources(), path => Path.GetFileName(path) == "SettingsControl.Profiles.cs");
        }

        /// <summary>
        /// plugin.md's Soon catalogue is the registry, row for row and in order: the page (", Add sheet" for an entry
        /// drawn only in a sheet), the row's title and its ticket, the ticket's link naming the same number. So a
        /// greyed row cannot be added, renamed, moved or closed in code without the doc moving (#544).
        /// </summary>
        [Fact]
        public void The_soon_catalogue_in_plugin_md_is_the_registry()
        {
            var doc = File.ReadAllText(Path.Combine(RepoPaths.Root(), "docs", "design", "plugin.md"));
            var start = doc.IndexOf("\n## Soon\n", StringComparison.Ordinal);
            Assert.True(start >= 0, "plugin.md has no Soon section");
            var end = doc.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
            var section = doc.Substring(start, (end < 0 ? doc.Length : end) - start);
            var rows = new List<string>();
            foreach (var line in section.Split('\n').Where(l => l.StartsWith("| ", StringComparison.Ordinal) && !l.StartsWith("| page |", StringComparison.Ordinal)))
            {
                var row = Regex.Match(line, @"^\| (?<page>[^|]+?) \| (?<title>[^|]+?) \| \[#(?<ticket>\d+)\]\(https://github\.com/xorob0/OpenDash/issues/(?<link>\d+)\) \|$");
                Assert.True(row.Success, "a Soon row reads page | row | [#n](link): " + line);
                Assert.Equal(row.Groups["ticket"].Value, row.Groups["link"].Value);
                rows.Add(row.Groups["page"].Value + " | " + row.Groups["title"].Value + " | " + row.Groups["ticket"].Value);
            }
            Assert.Equal(
                PanelSoon.All.Select(item => PageName(item.Page) + (item.InSheetOnly ? ", Add sheet" : "") + " | " + item.Title + " | " + item.Ticket),
                rows);
        }

        /// <summary>A page as the catalogue names it: the sidebar's word, which is "LEDs" where the enum says Leds.</summary>
        private static string PageName(PanelPage page)
        {
            return page == PanelPage.Leds ? "LEDs" : page.ToString();
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
