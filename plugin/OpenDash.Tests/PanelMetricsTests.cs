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
            Assert.Equal(Theme.Rule, PanelMetrics.SectionRule);
        }

        /// <summary>The card, as design/canvas/PluginComponents.dc.html draws it: min-width 152, height 58,
        /// padding 0 12, icon 10 from the text and the name 5 off the size.</summary>
        [Fact]
        public void The_screen_card_is_the_size_the_canvas_draws()
        {
            Assert.Equal(152, PanelMetrics.CardMinWidth);
            Assert.Equal(58, PanelMetrics.CardHeight);
            Assert.Equal(12, PanelMetrics.CardPaddingX);
            Assert.Equal(10, PanelMetrics.CardIconGap);
            Assert.Equal(5, PanelMetrics.CardLineGap);
            Assert.Equal(2, PanelMetrics.Radius);
        }

        [Fact]
        public void The_add_card_is_narrower_than_a_screen_card_and_as_tall()
        {
            Assert.Equal(132, PanelMetrics.AddCardMinWidth);
            Assert.Equal(8, PanelMetrics.AddCardGap);
            Assert.True(PanelMetrics.AddCardMinWidth < PanelMetrics.CardMinWidth,
                "the add card is the narrow one: " + PanelMetrics.AddCardMinWidth + " against " + PanelMetrics.CardMinWidth);
            Assert.True(PanelMetrics.DashOn > 0 && PanelMetrics.DashOff > 0,
                "the add card's frame is dashed, and that is the only thing telling it from a card at rest");
        }

        /// <summary>
        /// The selection cue, which is now quiet enough to be worth pinning: a uniform one pixel of accent
        /// on ui.field, against a card at rest that is transparent inside ui.border. The accent icon and
        /// the accent outline carry it alone -- the underline the card used to wear is gone.
        /// </summary>
        [Fact]
        public void A_card_says_it_is_selected_with_the_accent_and_nothing_else()
        {
            var selected = PanelMetrics.Card(true);
            Assert.Equal(Theme.Field, selected.Fill);
            Assert.Equal(Theme.Accent, selected.Border);
            Assert.Equal(Theme.Accent, selected.Icon);
            Assert.Equal(Theme.TextSecondary, selected.SizeLabel);

            var rest = PanelMetrics.Card(false);
            Assert.Null(rest.Fill);
            Assert.Equal(Theme.Border, rest.Border);
            Assert.Equal(Theme.TextLabel, rest.Icon);
            Assert.Equal(Theme.TextLabel, rest.SizeLabel);

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
            Assert.Equal(2, PanelMetrics.FocusRingWeight);
            Assert.Equal(2, PanelMetrics.FocusRingOffset);
            Assert.Equal(0.4, PanelMetrics.DisabledOpacity);
        }

        [Fact]
        public void The_section_is_the_canvas_twenty_eight_over_twenty()
        {
            Assert.Equal(28, PanelMetrics.SectionPadding);
            Assert.Equal(20, PanelMetrics.SectionGap);
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
    }
}
