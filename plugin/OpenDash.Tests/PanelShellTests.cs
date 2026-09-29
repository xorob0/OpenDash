// PanelShellTests.cs: the panel's frame, held to the redesign's artboards.
//
// The literals below are read off Sidebar.dc.html and the page artboards of the #503 canvas
// (https://claude.ai/artifact/J5n6q4b15MC4xp5J5r91QW). Those artboards are not under design/ yet, so this is
// a pin rather than a join: once the author copies them to design/canvas/plugin/*.dc.html, these move to
// reading the files the way PanelIconsTests reads PluginComponents.dc.html.
using System;
using System.Linq;
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
            // The divider: one pixel with eight above and below, and the column's 2 px gap on either side
            // of it, since the artboard draws it as an item of its own: 2 + 8 + 1 + 8 + 2.
            Assert.Equal(17, PanelShell.NavDividerHeight);
            Assert.Equal(21, PanelShell.NavDividerHeight + 2 * PanelShell.NavItemGap);
            // The label, the amber dot and the count, 12 apart; the live dot's 3 px ring at 18 %.
            Assert.Equal(12, PanelShell.NavTrailGap);
            Assert.Equal(3, PanelShell.LiveRingWidth);
            Assert.Equal(0.18, PanelShell.LiveRingOpacity);
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
            // The pages' padding: 36 above, 32 below.
            Assert.Equal(36, PanelShell.MainPaddingTop);
            Assert.Equal(32, PanelShell.MainPaddingBottom);
        }

        /// <summary>Each artboard's &lt;main&gt; puts its own gap between sections, and PageLayout reads it.</summary>
        [Fact]
        public void Each_page_spaces_its_sections_as_its_artboard_does()
        {
            // Main.dc.html's <main> gap 28, and Settings' .sec{padding-top:28px}.
            Assert.Equal(28, PanelShell.SectionGap);
            Assert.Equal(28, PanelShell.SectionGapFor(PanelPage.Home));
            Assert.Equal(28, PanelShell.SectionGapFor(PanelPage.Settings));
            // Screens, Leds, Matrix and Shortcuts: gap 22.
            Assert.Equal(22, PanelShell.SectionGapFor(PanelPage.Screens));
            Assert.Equal(22, PanelShell.SectionGapFor(PanelPage.Leds));
            Assert.Equal(22, PanelShell.SectionGapFor(PanelPage.Matrix));
            Assert.Equal(22, PanelShell.SectionGapFor(PanelPage.Shortcuts));
            // Updates 26, Rig 18.
            Assert.Equal(26, PanelShell.SectionGapFor(PanelPage.Updates));
            Assert.Equal(18, PanelShell.SectionGapFor(PanelPage.Rig));
        }

        /// <summary>The rail's inside is 39, not 40: its 56 less the rule and 8 each side. What it holds is
        /// drawn to that, and a capture script finds an item's x from the layout as it finds its y.</summary>
        [Fact]
        public void The_rail_holds_what_fits_inside_its_rule()
        {
            Assert.Equal(39, PanelShell.RailInnerWidth);
            // Night mode on the rail is an icon toggle the rail's inside wide, as the search button is.
            var sidebar = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.Sidebar.cs"));
            Assert.Contains("narrow ? RailNightToggle(", sidebar);
            Assert.Contains("Ui.NavIcon(PanelIcons.Night,", sidebar);
            // The focus ring is drawn whole: the items' viewport is widened by the ring and the items drawn
            // back in, the rail's search carries the kit's ring, and the badge keeps the 12 px trail gap.
            Assert.Contains("Margin = new Thickness(-FocusRingOutset, 0, -FocusRingOutset, 0)", sidebar);
            Assert.Contains("top.Margin = new Thickness(FocusRingOutset, 0, FocusRingOutset, 0);", sidebar);
            // The nav item, the rail's search and the rail's night toggle.
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(sidebar, @"FocusVisualStyle = Ui\.FocusRing\(\)").Count);
            Assert.Contains("tag.Margin = new Thickness(PanelShell.NavTrailGap, 0, 0, 0);", sidebar);
            Assert.Equal(107.5, PanelShell.ItemCentreX(PanelLayout.Full));
            Assert.Equal(27.5, PanelShell.ItemCentreX(PanelLayout.Rail));
            Assert.Equal(27.5, PanelShell.ItemCentreX(PanelLayout.Compact));
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
            // The main column's scroll bar comes out of the room wherever the room is under the ceiling:
            // a 1100 px window has 1100 - 216 - 88 - 17, and a 3840 one is still capped.
            Assert.Equal(1100 - 216 - 88 - 17, PanelShell.ContentWidth(1100, false, 17));
            Assert.Equal(1112, PanelShell.ContentWidth(3840, false, 17));
            Assert.Equal(3840 - 216 - 88 - 17, PanelShell.ContentWidth(3840, true, 17));
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
            // Screens comes after the rule under Rig, which is 17 and the gap after it, 19 in all.
            Assert.Equal(337, PanelShell.ItemCentre(2));
            // Shortcuts comes after the rule under Matrix as well.
            Assert.Equal(482, PanelShell.ItemCentre(5));
            Assert.Equal(524, PanelShell.ItemCentre(6));
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

        /// <summary>The rest of the sidebar's numbers, off Sidebar.dc.html.</summary>
        [Fact]
        public void The_rest_of_the_sidebar_is_the_artboards()
        {
            Assert.Equal(56, PanelShell.RailWidth);
            Assert.Equal(8, PanelShell.RailPaddingX);
            // The mark row: an 18 px mark, the 21 px wordmark and the 14 px version, 8 in.
            Assert.Equal(8, PanelShell.MarkRowPaddingX);
            Assert.Equal(18, PanelShell.MarkSize);
            Assert.Equal(21, PanelShell.WordmarkSize);
            Assert.Equal(14, PanelShell.VersionSize);
            // Search: 10 in, a 14 px lens, 13 px text.
            Assert.Equal(10, PanelShell.SearchPaddingX);
            Assert.Equal(14, PanelShell.SearchIconSize);
            Assert.Equal(13, PanelShell.SearchTextSize);
            // The live card: 12 in, lines 5 apart, rows of 14, 18 and 16, a 7 px dot; the rail's 22.
            Assert.Equal(12, PanelShell.LiveCardPaddingX);
            Assert.Equal(5, PanelShell.LiveLineGap);
            Assert.Equal(14, PanelShell.LiveEyebrowHeight);
            Assert.Equal(18, PanelShell.LiveCarHeight);
            Assert.Equal(16, PanelShell.LiveTrackHeight);
            Assert.Equal(7, PanelShell.LiveDotSize);
            Assert.Equal(22, PanelShell.RailLiveHeight);
            // An item's inside: 10 in, a 15 px count, a 7 px amber dot; the rule 10 in from either side.
            Assert.Equal(10, PanelShell.NavItemPaddingX);
            Assert.Equal(15, PanelShell.NavCountSize);
            Assert.Equal(7, PanelShell.NavWarnSize);
            Assert.Equal(8, PanelShell.NavDividerMarginY);
            Assert.Equal(10, PanelShell.NavDividerMarginX);
            // The foot: 14 above and below night mode, 8 in, Updates 4 under it, its badge 20 high, 6 in, 13 px.
            Assert.Equal(14, PanelShell.FootPaddingY);
            Assert.Equal(8, PanelShell.FootPaddingX);
            Assert.Equal(4, PanelShell.UpdatesGap);
            Assert.Equal(20, PanelShell.BadgeHeight);
            Assert.Equal(6, PanelShell.BadgePaddingX);
            Assert.Equal(13, PanelShell.BadgeTextSize);
        }

        /// <summary>What every page shares, off the page artboards' classes.</summary>
        [Fact]
        public void The_pages_share_the_artboards_type_and_rows()
        {
            // The page title at 30, .h2 at 17 and its larger 19, the sheet-like sub-heading at 22, .lbl at 11.
            Assert.Equal(30, PanelShell.PageTitleSize);
            Assert.Equal(17, PanelShell.HeadingSize);
            Assert.Equal(19, PanelShell.HeadingLargeSize);
            Assert.Equal(22, PanelShell.SubHeadingSize);
            Assert.Equal(11, PanelShell.EyebrowSize);
            // .row: a 15 px title, 12 above and below, 24 between the title and its control; a sub-row 16 in.
            Assert.Equal(15, PanelShell.RowTitleSize);
            Assert.Equal(12, PanelShell.RowPaddingY);
            Assert.Equal(24, PanelShell.RowGap);
            Assert.Equal(16, PanelShell.SubRowIndent);
            // .soontag and .new: 18 high, 6 in, 10 px; .crumb: 22 high, 7 in, 12 px; a message at 13.
            Assert.Equal(18, PanelShell.TagHeight);
            Assert.Equal(6, PanelShell.TagPaddingX);
            Assert.Equal(10, PanelShell.TagTextSize);
            Assert.Equal(22, PanelShell.CrumbHeight);
            Assert.Equal(7, PanelShell.CrumbPaddingX);
            Assert.Equal(12, PanelShell.CrumbTextSize);
            Assert.Equal(13, PanelShell.MessageTextSize);
            // .inp: 30 high, 10 in, 13 px; .num-in: 64 by 30, 8 in, 15 px.
            Assert.Equal(30, PanelShell.InputHeight);
            Assert.Equal(10, PanelShell.InputPaddingX);
            Assert.Equal(13, PanelShell.InputTextSize);
            Assert.Equal(64, PanelShell.NumberInputWidth);
            Assert.Equal(8, PanelShell.NumberInputPaddingX);
            Assert.Equal(15, PanelShell.NumberInputTextSize);
            // The main column's ceiling, and the room two columns need.
            Assert.Equal(1112, PanelShell.ContentMax);
            Assert.Equal(760, PanelShell.TwoColumnFrom);
        }

        /// <summary>The sheet, off AddScreen.dc.html and AddLeds.dc.html: 560 wide, 28 in, a 22 px title
        /// under 24 and over 18, a footer 18 above and 24 below, over a dim at 60 %.</summary>
        [Fact]
        public void The_sheet_is_the_artboards()
        {
            Assert.Equal(560, PanelShell.SheetWidthMax);
            Assert.Equal(900, PanelShell.SheetFullFrom);
            Assert.Equal(28, PanelShell.SheetPaddingX);
            Assert.Equal(24, PanelShell.SheetHeaderPaddingTop);
            Assert.Equal(18, PanelShell.SheetHeaderPaddingBottom);
            Assert.Equal(22, PanelShell.SheetTitleSize);
            Assert.Equal(18, PanelShell.SheetFooterPaddingTop);
            Assert.Equal(24, PanelShell.SheetFooterPaddingBottom);
            // Both Add sheets' body: padding 0 28px, nothing under the last step.
            Assert.Equal(0, PanelShell.SheetBodyPaddingBottom);
            Assert.Equal(0.6, PanelShell.SheetDimOpacity);
        }
    }
}
