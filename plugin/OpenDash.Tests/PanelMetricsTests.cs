// PanelMetricsTests.cs: the panel's geometry, held against the canvas the way ThemeTests holds Theme.cs
// against design/tokens.json.
//
// Widgets.cs is WPF and cannot be compiled here, which is the whole reason PanelMetrics.cs exists: a card
// that is the wrong size or an icon that is not the one the author drew is caught here rather than on the
// VM. Where a number is already a token, the test asserts the mirror rather than the literal, so that one
// change to design/tokens.json moves both.
using System;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelMetricsTests
    {
        [Fact]
        public void A_metric_that_has_a_token_mirrors_it_rather_than_repeating_the_number()
        {
            Assert.Equal(Theme.ControlHeightSm, PanelMetrics.PillHeight);
            Assert.Equal(Theme.ControlHeight, PanelMetrics.ButtonHeight);
            Assert.Equal(Theme.Radius, PanelMetrics.Radius);
            Assert.Equal(Theme.IconSize, PanelMetrics.IconSize);
            Assert.Equal(Theme.SurfaceZone, PanelMetrics.RowRule);
        }

        /// <summary>
        /// The dash a dashed frame is drawn with and the weight of every outline. (The old screen card's sizes
        /// and colours were pinned here too; the card went with #791, and the kit's cards, chips and steps are
        /// pinned in PanelKitTests.)
        /// </summary>
        [Fact]
        public void A_dashed_frame_has_a_dash_and_every_outline_is_one_pixel()
        {
            Assert.True(PanelMetrics.DashOn > 0 && PanelMetrics.DashOff > 0,
                "a dashed frame -- the add tile, Not bound -- is the only thing telling it from a card at rest");
            Assert.Equal(1, PanelMetrics.BorderWeight);
        }

        /// <summary>The paths are the canvas's own `d` strings, so they are compared character for
        /// character the way MarkTests compares MarkShape.PathData with media/logo.svg.</summary>
        [Fact]
        public void The_icons_are_the_paths_the_canvas_draws()
        {
            Assert.Equal("M2 3h12v8H2zM6 13.5h4", PanelMetrics.DisplayIcon);
            Assert.Equal("M2.5 2.5h4v4h-4zM9.5 2.5h4v4h-4zM2.5 9.5h4v4h-4zM9.5 9.5h4v4h-4z", PanelMetrics.GridIcon);
            Assert.Equal("M4.5 1.5h7v13h-7zM6.5 12.5h3", PanelMetrics.PhoneIcon);
            Assert.Equal("M8 3v10M3 8h10", PanelMetrics.PlusIcon);
        }

        [Fact]
        public void A_kind_is_drawn_with_the_icon_the_canvas_gives_it()
        {
            Assert.Equal(PanelMetrics.DisplayIcon, PanelMetrics.KindIcon(Contract.KindFace));
            Assert.Equal(PanelMetrics.GridIcon, PanelMetrics.KindIcon(Contract.KindPitWall));
            Assert.Equal(PanelMetrics.PhoneIcon, PanelMetrics.KindIcon(Contract.KindCompanion));
        }

        /// <summary>
        /// The one that has to fail when a fifth kind arrives. The canvas draws three icons and gives the
        /// slots screen none, so a kind the table has never heard of would be a card with no mark on it
        /// and nothing said in its place.
        /// </summary>
        [Fact]
        public void Every_kind_is_either_drawn_or_written_and_none_is_neither()
        {
            foreach (var kind in Contract.ScreenKinds)
            {
                Assert.True(PanelMetrics.KnowsKind(kind), kind + " has no entry in the card's kind table");
                var drawn = PanelMetrics.KindIcon(kind) != null;
                var written = PanelCopy.KindWord(kind) != null;
                Assert.True(drawn != written, kind + " is " + (drawn ? "both drawn and written" : "neither drawn nor written"));
            }

            Assert.Null(PanelMetrics.KindIcon(Contract.KindSlots));
            Assert.False(PanelMetrics.KnowsKind("windscreen"));
            Assert.Null(PanelMetrics.KindIcon(null));
        }

        [Fact]
        public void The_icons_are_distinct_so_that_two_kinds_never_read_the_same()
        {
            var paths = Contract.ScreenKinds.Select(PanelMetrics.KindIcon).Where(path => path != null).ToArray();
            Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
        }

        /// <summary>A 28 px button and a 24 px pill inside a 40 px row: the row has no vertical padding
        /// left to give, which is why it carries none.</summary>
        [Fact]
        public void An_install_row_is_tall_enough_for_the_button_and_the_pill_and_no_taller()
        {
            Assert.Equal(40, PanelMetrics.RowHeight);
            Assert.Equal(28, PanelMetrics.RowButtonHeight);
            Assert.Equal(12, PanelMetrics.RowIconGap);
            Assert.Equal(20, PanelMetrics.RowRightGap);
            Assert.True(PanelMetrics.RowButtonHeight < PanelMetrics.RowHeight, "a 28 button has to fit in the row");
            Assert.True(PanelMetrics.PillHeight < PanelMetrics.RowHeight, "so does a 24 pill");
            Assert.True(PanelMetrics.RowButtonHeight < PanelMetrics.ButtonHeight,
                "a button in a row is shorter than a button standing on its own");
        }

        /// <summary>
        /// A strip row that offers no Update holds an empty host where the button would go, and a package
        /// row holds no host at all; both end their pill on the same edge (#469).
        /// </summary>
        [Fact]
        public void A_row_whose_button_host_is_empty_is_as_wide_as_a_row_with_no_host()
        {
            const double pill = 112;
            var empty = PanelMetrics.RowOffsets(new[] { pill, 0.0 }, PanelMetrics.RowRightGap);
            var none = PanelMetrics.RowOffsets(new[] { pill }, PanelMetrics.RowRightGap);

            Assert.Equal(pill, empty[empty.Length - 1]);
            Assert.Equal(none[none.Length - 1], empty[empty.Length - 1]);
        }

        /// <summary>The margin HStack used to give put the pill at 0 and the button twenty past its end;
        /// the rule has to leave that row where it was.</summary>
        [Fact]
        public void A_row_that_shows_its_button_keeps_the_gap_before_it()
        {
            const double pill = 112, button = 96;

            Assert.Equal(
                new[] { 0, pill + 20, pill + 20 + button },
                PanelMetrics.RowOffsets(new[] { pill, button }, PanelMetrics.RowRightGap));
        }

        /// <summary>
        /// The plugin section's row puts a status, two buttons that are collapsed until they are wanted,
        /// and Reinstall side by side, and whichever are collapsed, one gap separates two that draw.
        /// </summary>
        [Fact]
        public void A_child_that_draws_nothing_neither_carries_a_gap_nor_leaves_one()
        {
            // Both buttons collapsed: the status and Reinstall, one gap apart.
            Assert.Equal(new[] { 0.0, 40, 40, 64, 124 }, PanelMetrics.RowOffsets(new[] { 40.0, 0, 0, 60 }, 24));
            // Update offered: a gap either side of it, and none for the collapsed button before it.
            Assert.Equal(new[] { 0.0, 40, 64, 138, 198 }, PanelMetrics.RowOffsets(new[] { 40.0, 0, 50, 60 }, 24));
            // Nothing drawn ahead of the first child that draws does not push it along.
            Assert.Equal(new[] { 0.0, 0, 30 }, PanelMetrics.RowOffsets(new[] { 0.0, 30 }, 8));
            Assert.Equal(new[] { 0.0 }, PanelMetrics.RowOffsets(new double[0], 8));
        }

        [Fact]
        public void A_status_is_a_six_pixel_dot_eight_from_its_label_in_a_pill_of_control_heightSm()
        {
            Assert.Equal(6, PanelMetrics.DotSize);
            Assert.Equal(8, PanelMetrics.PillGap);
            Assert.Equal(24, PanelMetrics.PillHeight);
        }

        [Fact]
        public void A_button_keeps_the_canvas_padding_and_draws_its_own_four_states()
        {
            Assert.Equal(16, PanelMetrics.ButtonPaddingX);
            Assert.Equal(8, PanelMetrics.ButtonIconGap);
            // Against Theme, which is what the control is actually drawn from: pinning the literal
            // let the two pairs drift apart for as long as nobody read both.
            Assert.Equal(Theme.FocusRing, PanelMetrics.FocusRingWeight);
            Assert.Equal(Theme.FocusRingOffset, PanelMetrics.FocusRingOffset);
            Assert.Equal(2, Theme.FocusRing);
            Assert.Equal(2, Theme.FocusRingOffset);
            Assert.Equal(0.4, PanelMetrics.DisabledOpacity);
        }

        [Fact]
        public void Progress_is_a_four_pixel_bar_under_its_own_line()
        {
            Assert.Equal(240, PanelMetrics.ProgressWidth);
            Assert.Equal(4, PanelMetrics.ProgressBarHeight);
            Assert.Equal(8, PanelMetrics.ProgressGap);
        }

        /// <summary>A run that reports more than it has done, or less than nothing, still draws a bar
        /// inside its own track.</summary>
        [Fact]
        public void A_fraction_outside_nought_to_one_neither_overruns_the_bar_nor_reverses_it()
        {
            Assert.Equal(62, PanelMetrics.PercentOf(0.62));
            Assert.Equal(0, PanelMetrics.PercentOf(-1));
            Assert.Equal(100, PanelMetrics.PercentOf(2));
            Assert.Equal(0, PanelMetrics.PercentOf(double.NaN));

            Assert.Equal(120, PanelMetrics.ProgressFill(0.5, 240));
            Assert.Equal(0, PanelMetrics.ProgressFill(-0.2, 240));
            Assert.Equal(240, PanelMetrics.ProgressFill(1.5, 240));
        }

        /// <summary>
        /// A field is padded as both panel sheets draw it, and they draw it the same way everywhere.
        /// </summary>
        /// <remarks>
        /// Read from the sheets rather than written down, because Widgets.cs carried 9 and 6 and said in
        /// a comment that the two were the canvas's own, which nothing compiled here could contradict.
        /// The shorthand is `padding: 0 <right>px 0 <left>px`, so the assertion reads it in that order.
        /// </remarks>
        [Fact]
        public void A_field_is_padded_as_both_panel_sheets_draw_it()
        {
            var seen = new System.Collections.Generic.HashSet<(string Right, string Left)>();
            var count = 0;
            foreach (var sheet in new[] { "Plugin.dc.html", "PluginComponents.dc.html" })
            {
                var path = System.IO.Path.Combine(RepoPaths.Root(), "design", "canvas", sheet);
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                             System.IO.File.ReadAllText(path), @"padding: 0 (\d+)px 0 (\d+)px"))
                {
                    seen.Add((m.Groups[1].Value, m.Groups[2].Value));
                    count++;
                }
            }

            Assert.True(count >= 20, $"the sheets drew a padded field {count} times");
            var only = Assert.Single(seen);
            Assert.Equal(PanelMetrics.FieldPaddingRight, double.Parse(only.Right, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(PanelMetrics.FieldPaddingLeft, double.Parse(only.Left, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
