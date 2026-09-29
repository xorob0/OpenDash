// PanelShellTests.cs: the panel's frame, held to the redesign's artboards.
//
// The literals below are read off Sidebar.dc.html and the page artboards of the #503 canvas
// (https://claude.ai/artifact/J5n6q4b15MC4xp5J5r91QW). Those artboards are not under design/ yet, so this is
// a pin rather than a join: once the author copies them to design/canvas/plugin/*.dc.html, these move to
// reading the files the way PanelIconsTests reads PluginComponents.dc.html.
using System;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelShellTests
    {
        [Fact]
        public void The_sidebar_is_drawn_as_the_artboard_draws_it()
        {
            // nav: width 216, padding 22/12/14.
            Assert.Equal(216, PanelShell.SidebarWidth);
            Assert.Equal(22, PanelShell.SidebarPaddingTop);
            Assert.Equal(12, PanelShell.SidebarPaddingX);
            Assert.Equal(14, PanelShell.SidebarPaddingBottom);
            // The header row: 21 px wordmark, 20 under it.
            Assert.Equal(21, PanelShell.MarkRowHeight);
            Assert.Equal(20, PanelShell.MarkRowGap);
            // Search: 34 high, 14 under it.
            Assert.Equal(34, PanelShell.SearchHeight);
            Assert.Equal(14, PanelShell.SearchGap);
            // The live card: 12/12/13 padding, 18 under it.
            Assert.Equal(12, PanelShell.LiveCardPaddingTop);
            Assert.Equal(13, PanelShell.LiveCardPaddingBottom);
            Assert.Equal(18, PanelShell.LiveCardGap);
            // An item: 40 high, 2 apart, an 18 px icon 12 from its label, 15 px text, a 2 px accent bar.
            Assert.Equal(40, PanelShell.NavItemHeight);
            Assert.Equal(2, PanelShell.NavItemGap);
            Assert.Equal(18, PanelIcons.NavSize);
            Assert.Equal(12, PanelShell.NavIconGap);
            Assert.Equal(15, PanelShell.NavTextSize);
            Assert.Equal(2, PanelShell.NavActiveBar);
            // The divider: one pixel with eight above and below.
            Assert.Equal(17, PanelShell.NavDividerHeight);
            // The switch: 40 by 22, a 16 px knob inset 3.
            Assert.Equal(40, PanelShell.SwitchWidth);
            Assert.Equal(22, PanelShell.SwitchHeight);
            Assert.Equal(16, PanelShell.SwitchKnob);
            Assert.Equal(3, PanelShell.SwitchInset);
        }

        [Fact]
        public void The_live_card_is_one_height_whatever_it_says()
        {
            var inside = PanelShell.LiveEyebrowHeight + PanelShell.LiveLineGap + PanelShell.LiveCarHeight + PanelShell.LiveLineGap + PanelShell.LiveTrackHeight;
            Assert.Equal(PanelShell.LiveCardHeight, inside + PanelShell.LiveCardPaddingTop + PanelShell.LiveCardPaddingBottom + 2);
        }

        [Theory]
        [InlineData(3840, PanelLayout.Full)]
        [InlineData(1600, PanelLayout.Full)]
        [InlineData(1000, PanelLayout.Full)]
        [InlineData(999, PanelLayout.Rail)]
        [InlineData(760, PanelLayout.Rail)]
        [InlineData(759, PanelLayout.Compact)]
        [InlineData(700, PanelLayout.Compact)]
        public void The_panel_reads_at_three_widths(double width, PanelLayout layout)
        {
            Assert.Equal(layout, PanelShell.Layout(width));
            Assert.Equal(layout != PanelLayout.Full, PanelShell.IsNarrow(layout));
        }

        [Fact]
        public void The_gutter_and_the_sidebar_narrow_with_the_window()
        {
            Assert.Equal(44, PanelShell.MainPaddingX(PanelLayout.Full));
            Assert.Equal(32, PanelShell.MainPaddingX(PanelLayout.Rail));
            Assert.Equal(20, PanelShell.MainPaddingX(PanelLayout.Compact));
            Assert.Equal(216, PanelShell.SidebarWidthFor(PanelLayout.Full));
            Assert.Equal(56, PanelShell.SidebarWidthFor(PanelLayout.Rail));
            Assert.Equal(56, PanelShell.SidebarWidthFor(PanelLayout.Compact));
            // The pages' padding: 36 above, 32 below, 28 between sections.
            Assert.Equal(36, PanelShell.MainPaddingTop);
            Assert.Equal(32, PanelShell.MainPaddingBottom);
            Assert.Equal(28, PanelShell.SectionGap);
        }

        [Fact]
        public void The_content_grows_to_its_ceiling_and_no_further_unless_it_is_the_rig()
        {
            // 1200: the artboard's own width, 1200 - 216 - 88.
            Assert.Equal(896, PanelShell.ContentWidth(1200));
            // 3840: capped.
            Assert.Equal(1112, PanelShell.ContentWidth(3840));
            Assert.Equal(3840 - 216 - 88, PanelShell.ContentWidth(3840, wide: true));
            // 900: the rail and a 32 gutter.
            Assert.Equal(900 - 56 - 64, PanelShell.ContentWidth(900));
            // 700: the rail and a 20 gutter.
            Assert.Equal(700 - 56 - 40, PanelShell.ContentWidth(700));
            Assert.Equal(0, PanelShell.ContentWidth(10));
        }

        [Fact]
        public void Two_columns_only_beside_the_full_sidebar_and_with_the_room()
        {
            Assert.True(PanelShell.TwoColumns(PanelLayout.Full, 760));
            Assert.False(PanelShell.TwoColumns(PanelLayout.Full, 759));
            Assert.False(PanelShell.TwoColumns(PanelLayout.Rail, 900));
            Assert.False(PanelShell.TwoColumns(PanelLayout.Compact, 900));
        }

        [Theory]
        // Home's three cards at the artboard's width: three of at least 240 with 16 between them.
        [InlineData(896, 240, 16, 3, PanelLayout.Full, 3)]
        [InlineData(700, 240, 16, 3, PanelLayout.Full, 2)]
        [InlineData(200, 240, 16, 3, PanelLayout.Full, 1)]
        [InlineData(4000, 240, 16, 3, PanelLayout.Full, 3)]
        // The narrowest layout holds two whatever the room.
        [InlineData(4000, 100, 16, 6, PanelLayout.Compact, 2)]
        [InlineData(0, 100, 16, 6, PanelLayout.Full, 1)]
        public void A_card_grid_takes_as_many_columns_as_fit(double available, double minCard, double gap, int max, PanelLayout layout, int columns)
        {
            Assert.Equal(columns, PanelShell.Columns(available, minCard, gap, max, layout));
        }

        [Fact]
        public void A_sheet_is_560_beside_the_cards_and_the_whole_column_below_900()
        {
            Assert.Equal(560, PanelShell.SheetWidth(1600));
            Assert.Equal(560, PanelShell.SheetWidth(900));
            Assert.Equal(899 - 56, PanelShell.SheetWidth(899));
            Assert.Equal(700 - 56, PanelShell.SheetWidth(700));
        }

        [Fact]
        public void The_items_sit_where_the_sidebar_draws_them()
        {
            // Home: 22 + 21 + 20 + 34 + 14 + 85 + 18 = 214 to its top, 20 more to its centre.
            Assert.Equal(234, PanelShell.ItemCentre(0));
            // Rig is one item on.
            Assert.Equal(234 + 42, PanelShell.ItemCentre(1));
            // Screens comes after the rule under Rig.
            Assert.Equal(234 + 2 * 42 + 17, PanelShell.ItemCentre(2));
            // Shortcuts comes after the rule under Matrix as well.
            Assert.Equal(234 + 5 * 42 + 2 * 17, PanelShell.ItemCentre(5));
            Assert.Equal(234 + 6 * 42 + 2 * 17, PanelShell.ItemCentre(6));
            // The rail's card is its dot.
            Assert.Equal(234 - 85 + 22, PanelShell.ItemCentre(0, PanelLayout.Rail));
            // Updates is pinned to the foot.
            Assert.Equal(900 - 14 - 20, PanelShell.UpdatesCentre(900));
            Assert.Throws<ArgumentOutOfRangeException>(() => PanelShell.ItemCentre(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => PanelShell.ItemCentre(-1));
        }

        [Fact]
        public void A_route_is_a_page_and_perhaps_a_row()
        {
            Assert.Equal(PanelRoute.To(PanelPage.Settings, "settings.lighting"), new PanelRoute(PanelPage.Settings, "settings.lighting"));
            Assert.NotEqual(PanelRoute.To(PanelPage.Settings), PanelRoute.To(PanelPage.Settings, "settings.lighting"));
            Assert.Null(new PanelRoute(PanelPage.Home, string.Empty).Anchor);
            Assert.Equal(PanelPage.Home, PanelRoute.Home.Page);
            Assert.Equal("Settings#settings.lighting", PanelRoute.To(PanelPage.Settings, "settings.lighting").ToString());
        }

        [Fact]
        public void A_message_is_drawn_in_the_ink_its_tone_asks_for()
        {
            Assert.Equal(Theme.TextSecondary, PanelMessage.Ink(PanelTone.Info));
            Assert.Equal(Theme.Caution, PanelMessage.Ink(PanelTone.Caution));
            Assert.Equal(Theme.Danger, PanelMessage.Ink(PanelTone.Danger));
            Assert.Equal(PanelTone.Info, PanelMessage.Of(true, "Added Rim.").Tone);
            Assert.Equal(PanelTone.Caution, PanelMessage.Of(false, "Install failed.").Tone);
            Assert.Equal(string.Empty, PanelMessage.Info(null).Text);
        }

        [Fact]
        public void What_is_not_built_yet_is_drawn_at_the_artboards_opacity()
        {
            Assert.Equal(0.45, PanelShell.SoonOpacity);
        }
    }
}
