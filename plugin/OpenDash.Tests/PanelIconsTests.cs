// PanelIconsTests.cs: the icons are drawn twice and have to stay one set.
//
// design/canvas/PluginComponents.dc.html is the source and Ui.Icon redraws each path in WPF, because WPF
// cannot render an SVG. The two now share one string rather than a transcription of one, so this file reads
// the sheet and compares -- the join MarkTests makes with media/logo.svg, for the same reason: a path is
// easy to change and easy to break, and nothing on the VM says which of the two copies moved.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelIconsTests
    {
        /// <summary>The plugin components artboard, which is the only sheet that describes the panel's
        /// icons. RepoPaths names the files several tests share; this one has a single reader.</summary>
        static string SheetPath() => Path.Combine(RepoPaths.Root(), "design", "canvas", "PluginComponents.dc.html");

        static string Sheet() => File.ReadAllText(SheetPath());

        /// <summary>
        /// The sheet's icon row, as a name to `d` table.
        /// </summary>
        /// <remarks>
        /// Read from the "icons · depth" section alone. Every chevron the sheet draws inside a select is
        /// the same path, and taking the whole file would map that one twice and name it nothing; the row
        /// is the only place an icon is drawn beside the word for it.
        /// </remarks>
        static Dictionary<string, string> IconsOnTheSheet()
        {
            var sheet = Sheet();
            var section = sheet.IndexOf("icons &middot; depth", StringComparison.Ordinal);
            if (section < 0) section = sheet.IndexOf("icons · depth", StringComparison.Ordinal);
            Assert.True(section >= 0, SheetPath() + " no longer has an icon row headed \"icons · depth\"");

            var row = sheet.Substring(section);
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(row, @"<path d=""(?<d>[^""]+)""></path></svg><div class=""tok"">(?<name>[^<]+)</div>"))
            {
                found[match.Groups["name"].Value] = match.Groups["d"].Value;
            }
            Assert.NotEmpty(found);
            return found;
        }

        [Fact]
        public void The_panel_draws_the_paths_the_sheet_declares()
        {
            var sheet = IconsOnTheSheet();
            Assert.Equal(PanelIcons.Install, sheet["install"]);
            Assert.Equal(PanelIcons.Refresh, sheet["refresh"]);
            Assert.Equal(PanelIcons.Chevron, sheet["chevron"]);
            Assert.Equal(PanelIcons.Check, sheet["check"]);
            Assert.Equal(PanelIcons.Alert, sheet["alert"]);
            Assert.Equal(PanelIcons.External, sheet["external"]);
            Assert.Equal(PanelIcons.Display, sheet["display"]);
            Assert.Equal(PanelIcons.Grid, sheet["grid"]);
        }

        /// <summary>
        /// The one that has to fail when the author draws a ninth icon.
        /// </summary>
        /// <remarks>
        /// An icon added to the sheet and not to this file is an icon the panel cannot draw, and nothing
        /// else would say so: the panel would simply go on drawing the eight it knows. The audit asked for
        /// eleven names, of which the sheet carries these eight; the phone and the plus are below, and the
        /// button icon it named is on no artboard at all.
        /// </remarks>
        [Fact]
        public void Every_icon_the_sheet_draws_has_a_constant_here()
        {
            var drawn = IconsOnTheSheet().Keys.OrderBy(name => name, StringComparer.Ordinal);
            Assert.Equal(new[] { "alert", "check", "chevron", "display", "external", "grid", "install", "refresh" }, drawn);
        }

        /// <summary>
        /// The phone and the plus, which the panel draws and the sheet does not.
        /// </summary>
        /// <remarks>
        /// Both were in the tree before the sheet was read against it, and both are on screen: the phone is
        /// the companion card's mark and the plus is the add card's. Neither appears on any artboard, so
        /// there is nothing to compare them with and this is a pin rather than a join. It fails if somebody
        /// edits the string, which is all it can promise.
        /// </remarks>
        [Fact]
        public void The_two_icons_no_artboard_declares_are_pinned_where_they_are()
        {
            Assert.Equal("M4.5 1.5h7v13h-7zM6.5 12.5h3", PanelIcons.Phone);
            Assert.Equal("M8 3v10M3 8h10", PanelIcons.Plus);

            var sheet = Sheet();
            Assert.DoesNotContain(PanelIcons.Phone, sheet, StringComparison.Ordinal);
            Assert.DoesNotContain(PanelIcons.Plus, sheet, StringComparison.Ordinal);
        }

        /// <summary>
        /// The rebuilt panel's own set (#503), pinned where it is.
        /// </summary>
        /// <remarks>
        /// The sidebar's eight are the `d` strings Sidebar.dc.html draws; Warning and Restart are Home's
        /// fix rows; Close, Search, the chevrons, the grip and the plus are the redesign's glyphs redrawn on
        /// the twenty unit box, because a typed "✕" or "›" is exactly what the test below forbids. None is on
        /// PluginComponents.dc.html, whose icon row stays eight, so this is a pin and not a join until the
        /// artboards are copied to design/canvas/plugin/.
        /// </remarks>
        [Fact]
        public void The_redesigns_icons_are_pinned_where_they_are()
        {
            Assert.Equal("M3 9l7-5.5L17 9v8H3z M8 17v-5h4v5", PanelIcons.Home);
            Assert.Equal("M2.5 3.5h9v6h-9z M13.5 3.5h4v4h-4z M2.5 12.5h15v3h-15z", PanelIcons.Rig);
            Assert.Equal("M2.5 4.5h15v9h-15z M7 17h6", PanelIcons.Screens);
            Assert.Equal("M2.5 8.5h3v3h-3z M8.5 8.5h3v3h-3z M14.5 8.5h3v3h-3z", PanelIcons.Leds);
            Assert.Equal("M3 3h4v4H3z M8 3h4v4H8z M13 3h4v4h-4z M3 8h4v4H3z M8 8h4v4H8z M13 8h4v4h-4z M3 13h4v4H3z M8 13h4v4H8z M13 13h4v4h-4z", PanelIcons.Matrix);
            Assert.Equal("M10 3a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M10 7.5a2.5 2.5 0 1 0 0 5a2.5 2.5 0 1 0 0-5z", PanelIcons.Shortcuts);
            Assert.Equal("M3 6h14 M3 14h14 M7 4v4 M13 12v4", PanelIcons.Settings);
            Assert.Equal("M10 3v9M6 8.5l4 4 4-4M3.5 14v3h13v-3", PanelIcons.Updates);
            Assert.Equal("M8.5 3a5.5 5.5 0 1 0 0 11a5.5 5.5 0 1 0 0-11z M12.5 12.5l4.5 4.5", PanelIcons.Search);
            Assert.Equal("M5 5l10 10M15 5L5 15", PanelIcons.Close);
            Assert.Equal("M10 3a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M10 6v5 M10 13.5v.5", PanelIcons.Warning);
            Assert.Equal("M15.5 6.5A6.5 6.5 0 1 0 16.5 11 M16 3v4h-4", PanelIcons.Restart);
            Assert.Equal("M7.5 4.5v1 M12.5 4.5v1 M7.5 9.5v1 M12.5 9.5v1 M7.5 14.5v1 M12.5 14.5v1", PanelIcons.DragHandle);
            Assert.Equal("M7.5 5l5 5-5 5", PanelIcons.ChevronRight);
            Assert.Equal("M5 7.5l5 5 5-5", PanelIcons.ChevronDown);
            Assert.Equal("M10 4v12M4 10h12", PanelIcons.Add);
            Assert.Equal("M16 12.5A7 7 0 1 1 7.5 4a6.5 6.5 0 0 0 8.5 8.5z", PanelIcons.Night);
            Assert.Equal(20, PanelIcons.NavBox);
            Assert.Equal(18, PanelIcons.NavSize);

            var sheet = Sheet();
            foreach (var path in RedesignPaths) Assert.DoesNotContain(path, sheet, StringComparison.Ordinal);
        }

        private static readonly string[] RedesignPaths =
        {
            PanelIcons.Home, PanelIcons.Rig, PanelIcons.Screens, PanelIcons.Leds, PanelIcons.Matrix, PanelIcons.Shortcuts,
            PanelIcons.Settings, PanelIcons.Updates, PanelIcons.Search, PanelIcons.Close, PanelIcons.Warning, PanelIcons.Restart,
            PanelIcons.DragHandle, PanelIcons.ChevronRight, PanelIcons.ChevronDown, PanelIcons.Add, PanelIcons.Night,
        };

        /// <summary>The set is one hand: one box, one weight, and two sizes named by the sheet's own
        /// caption rather than chosen here.</summary>
        [Fact]
        public void An_icon_is_sixteen_in_a_control_and_twenty_standing_alone()
        {
            Assert.Contains("16 in controls, 20 alone, 1.5 stroke, square caps, mitre joins.", Sheet(), StringComparison.Ordinal);
            Assert.Equal(16, PanelIcons.SizeInControl);
            Assert.Equal(20, PanelIcons.SizeAlone);
            Assert.Equal(1.5, PanelIcons.StrokeWeight);
            Assert.Equal(16, PanelIcons.Box);
            Assert.Equal(Theme.IconSize, PanelIcons.SizeInControl);
        }

        /// <summary>Every path is drawn on the box, and the sheet says so on each icon rather than once
        /// for the row, so a path that strayed outside it would still be scaled as though it had not.</summary>
        [Fact]
        public void Every_path_is_declared_on_the_sixteen_unit_box()
        {
            var row = Sheet();
            var section = row.IndexOf("icons · depth", StringComparison.Ordinal);
            if (section < 0) section = row.IndexOf("icons &middot; depth", StringComparison.Ordinal);
            // The sheet is CSS and SVG, which write a decimal point whatever the machine's locale is; the
            // constants have to be spelled the same way to be looked for in it.
            var box = PanelIcons.Box.ToString(CultureInfo.InvariantCulture);
            var stroke = PanelIcons.StrokeWeight.ToString(CultureInfo.InvariantCulture);
            foreach (Match svg in Regex.Matches(row.Substring(section), @"<svg[^>]*>"))
            {
                Assert.Contains("viewBox=\"0 0 " + box + " " + box + "\"", svg.Value, StringComparison.Ordinal);
                Assert.Contains("stroke-width=\"" + stroke + "\"", svg.Value, StringComparison.Ordinal);
                Assert.Contains("stroke-linecap=\"square\"", svg.Value, StringComparison.Ordinal);
                Assert.Contains("stroke-linejoin=\"miter\"", svg.Value, StringComparison.Ordinal);
                Assert.Contains("fill=\"none\"", svg.Value, StringComparison.Ordinal);
            }
        }

        /// <summary>No two icons are the same shape, or two things on the panel read as one.</summary>
        [Fact]
        public void The_paths_are_distinct()
        {
            var paths = new[]
            {
                PanelIcons.Install, PanelIcons.Refresh, PanelIcons.Chevron, PanelIcons.Check,
                PanelIcons.Alert, PanelIcons.External, PanelIcons.Display, PanelIcons.Grid,
                PanelIcons.Phone, PanelIcons.Plus,
            }.Concat(RedesignPaths).ToArray();
            Assert.Equal(27, paths.Length);
            Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
        }

        /// <summary>
        /// The Install tab's Reinstall button, as design/canvas/Plugin.dc.html draws it.
        /// </summary>
        /// <remarks>
        /// The component sheet spells the word "Reinstall" on its primary swatches, so a reader who has
        /// only seen that sheet concludes this button is the tab's accented press and carries the install
        /// icon. The page itself says otherwise on both counts, and the page is the drawing of the thing
        /// rather than of the style; this holds the panel to it, since the button is WPF and no other test
        /// here can compile a line of it.
        /// </remarks>
        [Fact]
        public void The_reinstall_button_is_the_outline_the_page_draws_with_the_refresh_icon()
        {
            var page = File.ReadAllText(Path.Combine(RepoPaths.Root(), "design", "canvas", "Plugin.dc.html"));
            var drawn = Regex.Matches(page, @"<div class=""btn"" style=""(?<style>[^""]*)""><svg[^>]*stroke=""(?<ink>#[0-9A-Fa-f]{6})""[^>]*><path d=""(?<d>[^""]+)""></path></svg>Reinstall</div>");
            Assert.Single(drawn);

            var button = drawn[0];
            Assert.Equal(PanelIcons.Refresh, button.Groups["d"].Value);
            Assert.Equal(Theme.TextPrimary, button.Groups["ink"].Value);

            var style = button.Groups["style"].Value;
            Assert.Contains("background: transparent", style, StringComparison.Ordinal);
            Assert.Contains("color: " + Theme.TextPrimary, style, StringComparison.Ordinal);
            Assert.Contains("border: 1px solid " + Theme.Border, style, StringComparison.Ordinal);
            Assert.DoesNotContain(Theme.Accent, style, StringComparison.Ordinal);
        }

        /// <summary>
        /// No icon is typed: a text factory is never handed a glyph to stand in for a drawn path.
        /// </summary>
        /// <remarks>
        /// The add card carried <c>Text("+", 20, FontWeights.Light, Theme.Accent)</c> for as long as it
        /// existed, and nothing said so, because the panel is WPF and neither check command compiles a
        /// line of it. A typed icon is not merely the wrong shape: it is scaled by the font's metrics
        /// rather than by the box, it carries the face's own stroke instead of the 1.5 every drawn icon
        /// shares, and it moves off the row's centre whenever the face changes. That is why this reads
        /// the sources as text, which is the only hold available on a file no test can compile; it is
        /// the same reason RepoPaths already reads Theme.cs rather than referencing it.
        ///
        /// A glyph is recognised as a literal with neither a letter nor a digit in it, so the emoji the
        /// requirement also forbids fail here as well. The empty string is allowed, since several labels
        /// are built empty and filled once telemetry arrives.
        ///
        /// Every factory of the kit counts, not only Text, Label and Numeral: the pages draw most of their
        /// words through PageTitle, Prose, Chip, Crumbs, Button and the rows, and a "—" handed to any of
        /// them is the same typed icon. An argument that is a literal on its own is read wherever it sits
        /// in the call, so <c>Ui.Crumbs("Devices", "›")</c> fails, and so does a literal that starts an
        /// argument even when more is joined after it (<c>Ui.Text("› " + name)</c>), which is a typed icon
        /// leading a word; a literal joined into the middle of a longer string (a " · " between two names)
        /// is a separator and is not held. Text, Label and Numeral are read whatever class qualifies them,
        /// so a helper of another name cannot carry a glyph past the guard.
        /// </remarks>
        [Fact]
        public void The_panel_never_builds_an_icon_out_of_text()
        {
            var sources = Directory.GetFiles(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash"), "*.cs");
            Assert.NotEmpty(sources);

            var typed = new List<string>();
            foreach (var source in sources)
            {
                typed.AddRange(TypedGlyphs(File.ReadAllText(source)).Select(found => Path.GetFileName(source) + ": " + found));
            }

            Assert.True(typed.Count == 0,
                "an icon is drawn and never typed, but a glyph is handed to a text factory in:" +
                Environment.NewLine + string.Join(Environment.NewLine, typed));
        }

        /// <summary>
        /// The glyphs handed to a text factory in one source: every Ui.* factory, and Text, Label and Numeral
        /// under any qualifier, with each argument that is a literal or starts with one.
        /// </summary>
        private static List<string> TypedGlyphs(string text)
        {
            var typed = new List<string>();
            foreach (Match call in Regex.Matches(text, @"\b(?:\w+\.)?(?:Text|Label|Numeral)\s*\(|\bUi\.[A-Z]\w*\s*\("))
            {
                foreach (var literal in LiteralArguments(text, call.Index + call.Length))
                {
                    if (literal.Length > 0 && !literal.Any(char.IsLetterOrDigit)) typed.Add(call.Value + "\"" + literal + "\"");
                }
            }
            return typed;
        }

        /// <summary>The kit's factories are all held, not just the three this test was first written for,
        /// and the cases the first guard caught are still caught.</summary>
        [Fact]
        public void The_glyph_guard_reads_every_factory_and_every_argument()
        {
            Assert.Equal(new[] { "›" }, LiteralArguments("Ui.Crumbs(\"Devices\", \"›\");", "Ui.Crumbs(".Length).Where(l => !l.Any(char.IsLetterOrDigit)).ToArray());
            Assert.Equal(new[] { "—" }, LiteralArguments("Ui.Prose(\"—\", 13);", "Ui.Prose(".Length).ToArray());
            Assert.Empty(LiteralArguments("Ui.Text(a + \" · \" + b, 13);", "Ui.Text(".Length));
            Assert.Empty(LiteralArguments("Ui.Chip(Name(\"+\"), false, null);", "Ui.Chip(".Length));

            // A glyph leading a joined argument is a typed icon beside a word.
            Assert.Single(TypedGlyphs("Ui.Text(\"› \" + name, 13);"));
            Assert.Single(TypedGlyphs("Ui.Label(\"— \" + value);"));
            // Text, Label and Numeral under any qualifier, not only Ui's.
            Assert.Single(TypedGlyphs("Kit.Text(\"—\", 13);"));
            Assert.Single(TypedGlyphs("Text(\"—\", 13);"));
            Assert.Single(TypedGlyphs("Ui.Crumbs(\"Devices\", \"›\");"));
            // A separator between two words, and a word, pass.
            Assert.Empty(TypedGlyphs("Ui.Text(a + \" · \" + b, 13);"));
            Assert.Empty(TypedGlyphs("Ui.Text(\"Laps · \" + count, 13);"));
            Assert.Empty(TypedGlyphs("SetText(\"—\");"));
        }

        /// <summary>
        /// The arguments of the call that opens just before <paramref name="start"/> which are a string
        /// literal and nothing else, or which start with one (the literal is returned), read to the call's
        /// closing parenthesis.
        /// </summary>
        private static List<string> LiteralArguments(string text, int start)
        {
            var literals = new List<string>();
            var depth = 1;
            var argument = new System.Text.StringBuilder();
            string only = null;
            string leading = null;
            var parts = 0;
            for (var i = start; i < text.Length && depth > 0; i++)
            {
                var c = text[i];
                if (c == '"')
                {
                    var end = i + 1;
                    var value = new System.Text.StringBuilder();
                    while (end < text.Length && text[end] != '"')
                    {
                        if (text[end] == '\\' && end + 1 < text.Length) { value.Append(text[end + 1]); end += 2; continue; }
                        value.Append(text[end]);
                        end++;
                    }
                    if (depth == 1)
                    {
                        if (parts == 0) leading = value.ToString();
                        only = value.ToString();
                        parts++;
                    }
                    i = end;
                    continue;
                }
                if (c == '(' || c == '[' || c == '{') { depth++; if (depth == 2) parts++; continue; }
                if (c == ')' || c == ']' || c == '}') { depth--; if (depth > 0) continue; }
                if (depth == 1 && c != ',' ) { if (!char.IsWhiteSpace(c)) parts++; continue; }
                if (depth == 1 || depth == 0)
                {
                    if (parts == 1 && only != null) literals.Add(only);
                    else if (leading != null) literals.Add(leading);
                    only = null;
                    leading = null;
                    parts = 0;
                    argument.Clear();
                }
            }
            return literals;
        }

        /// <summary>The four names the cards ask PanelMetrics for are these paths and not a second copy
        /// of them.</summary>
        [Fact]
        public void The_card_asks_for_the_same_paths_the_sheet_declares()
        {
            Assert.Equal(PanelIcons.Display, PanelMetrics.DisplayIcon);
            Assert.Equal(PanelIcons.Grid, PanelMetrics.GridIcon);
            Assert.Equal(PanelIcons.Phone, PanelMetrics.PhoneIcon);
            Assert.Equal(PanelIcons.Plus, PanelMetrics.PlusIcon);
        }
    }
}
