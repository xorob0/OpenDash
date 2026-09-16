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
            };
            Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
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
