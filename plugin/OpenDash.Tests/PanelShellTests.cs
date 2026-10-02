// PanelShellTests.cs: the panel's frame, held to the redesign's artboards.
//
// The literals below are read off Sidebar.dc.html and the page artboards of the #503 canvas
// (https://claude.ai/artifact/J5n6q4b15MC4xp5J5r91QW). Those artboards are not under design/ yet, so this is
// a pin rather than a join: once the author copies them to design/canvas/plugin/*.dc.html, these move to
// reading the files the way PanelIconsTests reads PluginComponents.dc.html.
using System;
using System.Linq;
using System.Text.RegularExpressions;
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

        /// <summary>The clock's rule as amended (#523): a tick may read in-memory settings, SimHub's unit strings
        /// included, and never a device, a profile or the disk.</summary>
        [Fact]
        public void A_tick_reads_memory_and_never_a_device_a_profile_or_the_disk()
        {
            var live = System.Text.RegularExpressions.Regex.Replace(System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Live.cs")), @"\s*//\s*", " ");
            Assert.DoesNotContain("Nothing SimHub is asked on the tick", live);
            Assert.Contains("A tick reads what is held in memory, SimHub's own settings included", live);
            Assert.Contains("and never a device, a profile or the disk", live);
        }

        /// <summary>A rebuild in place puts keyboard focus back and leaves the scroll where the driver had it:
        /// focusing raises BringIntoView, which pulled the page back to the last control pressed (#523).</summary>
        [Fact]
        public void A_rebuild_keeps_the_scroll_where_the_driver_left_it()
        {
            var shell = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.cs")), @"\s+", " ");
            Assert.Contains("var offset = mainScroll.VerticalOffset; pageHost.Content = BuildPage(route); mainScroll.ScrollToVerticalOffset(offset); if (focus != null) RestoreFocus(pageHost, focus, offset);", shell);
            Assert.Contains("if (last != null) Keyboard.Focus(last); else pageHost.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); }", shell);
            Assert.Contains("if (spoke) return; mainScroll.ScrollToVerticalOffset(offset); Dispatcher.BeginInvoke(new Action(() => { if (saidCount == said) mainScroll.ScrollToVerticalOffset(offset); }), DispatcherPriority.Loaded);", shell);
        }

        /// <summary>
        /// A line said after a rebuild stays in view: nearly every press runs Redraw and then Say, and the
        /// rebuild's queued focus hand-back used to put the old offset back after Say had scrolled to the top, so
        /// a press below the fold wrote its outcome above the visible area. Say counts its lines; the hand-back
        /// compares the count it was queued at, and where a line has been said since it focuses without bringing
        /// the control into view and leaves the scroll alone.
        /// </summary>
        [Fact]
        public void A_line_said_after_a_rebuild_stays_in_view()
        {
            var shell = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.cs")), @"\s+", " ");
            var messages = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.Messages.cs")), @"\s+", " ");
            Assert.Contains("messageHost.Margin = new Thickness(0, 0, 0, 12); saidCount++; mainScroll.ScrollToTop();", messages);
            var restore = shell.Substring(shell.IndexOf("private void RestoreFocus(", System.StringComparison.Ordinal));
            restore = restore.Substring(0, restore.IndexOf("private FrameworkElement BuildPage(", System.StringComparison.Ordinal));
            var order = new[]
            {
                "var said = saidCount; Dispatcher.BeginInvoke(",
                "var spoke = saidCount != said;",
                "if (spoke) pageHost.AddHandler(FrameworkElement.RequestBringIntoViewEvent, stay);",
                "if (last != null) Keyboard.Focus(last);",
                "if (spoke) pageHost.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent, stay);",
                "if (spoke) return;",
                "mainScroll.ScrollToVerticalOffset(offset);",
            };
            var at = -1;
            foreach (var pin in order)
            {
                var found = restore.IndexOf(pin, at + 1, System.StringComparison.Ordinal);
                Assert.True(found > at, "RestoreFocus no longer carries, in order: " + pin);
                at = found;
            }
            Assert.Contains("RequestBringIntoViewEventHandler stay = (sender, args) => args.Handled = true;", restore);
        }

        /// <summary>A repeat of the click that opened or closed the sheet is dropped on the control's root, until a
        /// fresh press clears it (#523): a double-click on an Add tile leaves the sheet open, and a sheet's press
        /// does not reach the page drawn again under the pointer.</summary>
        [Fact]
        public void A_double_click_that_opened_the_sheet_does_not_close_it()
        {
            Assert.True(PanelShell.SheetSwallowsPress(true, 2));
            Assert.True(PanelShell.SheetSwallowsPress(true, 3));
            Assert.False(PanelShell.SheetSwallowsPress(true, 1));
            Assert.False(PanelShell.SheetSwallowsPress(false, 2));
            var sheet = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.Sheet.cs")), @"\s+", " ");
            Assert.Contains("PreviewMouseLeftButtonDown += (sender, args) => { if (PanelShell.SheetSwallowsPress(sheetMoved, args.ClickCount)) { args.Handled = true; return; } sheetMoved = false; };", sheet);
            Assert.Contains("sheetLayer.Visibility = Visibility.Visible; SheetMoved();", sheet);
            Assert.Contains("sheetLayer.Visibility = Visibility.Collapsed; sheetPanel.Child = null; SheetMoved();", sheet);
        }

        /// <summary>The flag box's by-hand route wraps its path box under the press where a column has no room
        /// for both (about 555 px), and keeps a copy's answer across a rebuild until the page is left (#523).</summary>
        [Fact]
        public void The_by_hand_route_wraps_and_keeps_its_answer_across_a_rebuild()
        {
            var profiles = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.Profiles.cs")), @"\s+", " ");
            Assert.Contains("var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, -PanelShell.ImportGap) };", profiles);
            Assert.Contains("copy.Margin = new Thickness(0, 0, PanelShell.ImportGap, PanelShell.ImportGap);", profiles);
            Assert.Contains("Margin = new Thickness(0, 0, 0, PanelShell.ImportGap),", profiles);
            Assert.DoesNotContain("Ui.HStack(12, copy, path)", profiles);
            Assert.Contains("OnLeave(\"FlagBox.importCopy\", () => { flagBoxCopied = null; flagBoxCopiedPath = null; });", profiles);
            Assert.Contains("flagBoxLine = Ui.Caption(flagBoxCopied ?? FlagBoxInstallPlan.Summary(plan, plugin.FlagBox?.Path), BodyWidth);", profiles);
            Assert.Contains("Text = flagBoxCopiedPath ?? plugin.FlagBox?.Path ?? string.Empty,", profiles);
        }

        /// <summary>A page's title takes a tag after it in the shell's layout (#523), 12 apart as Rig.dc.html and
        /// Map.dc.html draw New, so no page rebuilds the title PageLayout drew to add one.</summary>
        [Fact]
        public void A_page_title_takes_its_tag_in_the_shells_layout()
        {
            Assert.Equal(12, PanelShell.TitleTagGap);
            Assert.Equal(PanelShell.TitleTagGap, PanelRigMap.TitleTagGap);
            var shell = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.cs")), @"\s+", " ");
            Assert.Contains("private FrameworkElement PageLayout(string title, FrameworkElement actions, params UIElement[] sections) { return TaggedPageLayout(title, null, actions, sections); }", shell);
            Assert.Contains("var head = actions == null ? PageTitleRow(title, tag) : Ui.Row(PageTitleRow(title, tag), actions);", shell);
            Assert.Contains("if (tag == null) return Ui.PageTitle(title); return Ui.HStack(PanelShell.TitleTagGap, Ui.PageTitle(title), tag);", shell);
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
        /// <summary>How long a resize and a burst of wheel presses are let settle before a page is rebuilt.
        /// Page agents take LightingSettleMs as the coalescing hook, so a change is a decision, not a drift.</summary>
        [Fact]
        public void A_resize_and_a_lighting_burst_settle_before_a_rebuild()
        {
            Assert.Equal(150, PanelShell.ResizeSettleMs);
            Assert.Equal(120, PanelShell.LightingSettleMs);
        }

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

        /// <summary>
        /// The column takes all the width SimHub gives the panel. The artboards are drawn 1200 wide as a
        /// frame, not as a maximum: at 1200 the column is their 896, and past it the column keeps growing,
        /// left-aligned beside the sidebar, with nothing centred and no ceiling.
        /// </summary>
        [Fact]
        public void The_content_takes_the_whole_width_right_of_the_sidebar()
        {
            // 1200: the artboard's own frame, 1200 - 216 - 88.
            Assert.Equal(896, PanelShell.ContentWidth(1200));
            // 3840: no ceiling. The 1112 the foundation capped at left two thirds of a 4K window empty.
            Assert.Equal(3840 - 216 - 88, PanelShell.ContentWidth(3840));
            // 900: the rail and a 32 gutter.
            Assert.Equal(900 - 56 - 64, PanelShell.ContentWidth(900));
            // 700: the rail and a 20 gutter.
            Assert.Equal(700 - 56 - 40, PanelShell.ContentWidth(700));
            Assert.Equal(0, PanelShell.ContentWidth(10));
            // The main column's scroll bar comes out of the room at every width.
            Assert.Equal(1100 - 216 - 88 - 17, PanelShell.ContentWidth(1100, 17));
            Assert.Equal(3840 - 216 - 88 - 17, PanelShell.ContentWidth(3840, 17));
        }

        /// <summary>Nothing in the panel holds the column back or centres it: the main column's frame, the hosts
        /// inside it and the scroll around it are never given a width, a MaxWidth or an alignment other than
        /// the frame's Stretch, in any of the shell's files, and no page is wider than the rest.</summary>
        [Fact]
        public void The_shell_neither_caps_nor_centres_the_column()
        {
            var shell = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.cs"));
            Assert.Contains("mainScroll.Content = mainFrame;", shell);
            Assert.Contains("private readonly Border mainFrame = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };", shell);
            Assert.DoesNotContain("WidePage", shell);
            Assert.Null(typeof(PanelShell).GetField("ContentMax"));

            var code = string.Concat(RepoPaths.SettingsControlCode());
            const string hosts = @"\b(?:mainFrame|pageHost|messageHost|mainScroll)";
            // A width or a ceiling set on one of them after it is made.
            Assert.Empty(Regex.Matches(code, hosts + @"\.(?:Max|Min)?Width\s*=(?!=)"));
            Assert.Empty(Regex.Matches(code, hosts + @"\.(?:HorizontalAlignment|HorizontalContentAlignment)\s*=(?!=)"));
            // Or in the initializer it is made with: only the frame's Stretch and the page host's Stretch.
            foreach (Match made in Regex.Matches(code, hosts + @" = new \w+\s*(?:\([^)]*\))?\s*\{([^}]*)\}"))
            {
                var init = made.Groups[1].Value;
                Assert.DoesNotMatch(@"\b(?:Max|Min)?Width\s*=", init);
                foreach (Match alignment in Regex.Matches(init, @"\bHorizontal(?:Content)?Alignment\s*=\s*([\w.]+)"))
                {
                    Assert.Equal("HorizontalAlignment.Stretch", alignment.Groups[1].Value);
                }
            }
        }

        /// <summary>
        /// A resize rebuilds only a page drawn from what moved. With no ceiling the content width follows the
        /// control pixel for pixel, and rebuilding on every pixel reloaded Screens' live dashboard and walked
        /// SimHub's devices at every drag of a 4K window.
        /// </summary>
        [Fact]
        public void A_resize_rebuilds_only_a_page_drawn_from_what_moved()
        {
            const double bar = 17;
            const double whole = double.PositiveInfinity;
            // 3000 to 3840: Rig's canvas read the whole width and is laid out again.
            Assert.True(PanelShell.RebuildsOnResize(3000, 3840, whole, bar));
            // A page that never read the width (Home) is left alone: its rows and grids stretch without it.
            Assert.False(PanelShell.RebuildsOnResize(3000, 3840, 0, bar));
            // A page that read it only up to the preview's 880 is left alone past it...
            Assert.False(PanelShell.RebuildsOnResize(3000, 3840, 880, bar));
            Assert.False(PanelShell.RebuildsOnResize(1300, 3840, 880, bar));
            // ...and rebuilt below it, where the preview is drawn at the column.
            Assert.True(PanelShell.RebuildsOnResize(1100, 1180, 880, bar));
            // Under a pixel is not a move.
            Assert.False(PanelShell.RebuildsOnResize(3000, 3000.5, whole, bar));
            // TwoColumns flipping rebuilds a page that read nothing, at about 1081 px...
            Assert.True(PanelShell.RebuildsOnResize(1050, 1100, 0, bar));
            // ...and so does the layout, the rail to the full sidebar and the rail to the compact gutter.
            Assert.True(PanelShell.RebuildsOnResize(900, 1000, 0, bar));
            Assert.True(PanelShell.RebuildsOnResize(800, 700, 0, bar));
            Assert.False(PanelShell.RebuildsOnResize(800, 900, 0, bar));

            // The shell asks the rule, reading the width counts only when a build reads it, and a build starts
            // having read none.
            var shell = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.cs"));
            Assert.Contains("return PanelShell.RebuildsOnResize(builtControlWidth, controlWidth, builtWidthRead, SystemParameters.VerticalScrollBarWidth);", shell);
            Assert.Contains("private double ContentWidth => ContentWidthUpTo(double.PositiveInfinity);", shell);
            Assert.Contains("private bool TwoColumns => PanelShell.TwoColumns(layout, ColumnRoom);", shell);
            var build = shell.Substring(shell.IndexOf("private FrameworkElement BuildPage(PanelRoute to)", StringComparison.Ordinal));
            build = build.Substring(0, build.IndexOf("switch (to.Page)", StringComparison.Ordinal));
            Assert.Contains("builtWidthRead = 0;", build);
            // Only a page's own build reads ContentWidth: the shell reads ColumnRoom, which does not count, and
            // names ContentWidth only to declare it.
            string Code(string name) => RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == name));
            int Reads(string text) => Regex.Matches(text, @"\bContentWidth\b(?!\s*\()").Count;
            Assert.Equal(1, Reads(shell));
            Assert.Equal(0, Reads(Code("SettingsControl.Sheet.cs")));
            Assert.Equal(0, Reads(Code("SettingsControl.Sidebar.cs")));
            Assert.Equal(0, Reads(Code("SettingsControl.Messages.cs")));
        }

        [Fact]
        public void Two_columns_beside_the_full_sidebar_wherever_there_is_the_room()
        {
            Assert.True(PanelShell.TwoColumns(PanelLayout.Full, 760));
            Assert.False(PanelShell.TwoColumns(PanelLayout.Full, 759));
            Assert.False(PanelShell.TwoColumns(PanelLayout.Rail, 900));
            Assert.False(PanelShell.TwoColumns(PanelLayout.Compact, 900));
            // With no ceiling it holds from about 1080 px up, a 4K window included, scroll bar and all.
            Assert.False(PanelShell.TwoColumns(PanelShell.Layout(1000), PanelShell.ContentWidth(1000, 17)));
            Assert.True(PanelShell.TwoColumns(PanelShell.Layout(1200), PanelShell.ContentWidth(1200, 17)));
            Assert.True(PanelShell.TwoColumns(PanelShell.Layout(3840), PanelShell.ContentWidth(3840, 17)));
        }

        /// <summary>Prose keeps a measure: a caption or a paragraph wraps at .cap's 620 by default however wide
        /// the column, set against its left edge.</summary>
        [Fact]
        public void Prose_keeps_a_readable_measure()
        {
            Assert.Equal(620, PanelShell.ProseMaxWidth);
            string Code(string name) => RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", name));
            Assert.Contains("public static TextBlock Caption(string text, double maxWidth = PanelShell.ProseMaxWidth)", Code("Widgets.cs"));
            var kit = Code("Widgets.Kit.cs");
            var prose = kit.Substring(kit.IndexOf("public static TextBlock Prose(", StringComparison.Ordinal));
            prose = prose.Substring(0, prose.IndexOf("return block;", StringComparison.Ordinal));
            Assert.Contains("block.MaxWidth = PanelShell.ProseMaxWidth;", prose);
            Assert.Contains("block.HorizontalAlignment = HorizontalAlignment.Left;", prose);
        }

        [Theory]
        // Home's three cards at the artboard's width: three of at least 240 with 16 between them.
        [InlineData(896, 240, 16, 3, PanelLayout.Full, 3)]
        [InlineData(700, 240, 16, 3, PanelLayout.Full, 2)]
        [InlineData(200, 240, 16, 3, PanelLayout.Full, 1)]
        [InlineData(4000, 240, 16, 3, PanelLayout.Full, 3)]
        // Screens' six at a 4K column: every column in use, the cards stretching to fill the row.
        [InlineData(3840 - 216 - 88 - 17, 138, 10, 6, PanelLayout.Full, 6)]
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

        /// <summary>
        /// Tab goes the way the eye does. The sheet is laid out as title, body, footer, and opens on the
        /// body's first control, not on Close; the sidebar's foot follows its items; and neither scroll nor the
        /// page's host is an invisible tab stop before the first control.
        /// </summary>
        [Fact]
        public void The_sheet_and_the_sidebar_tab_in_reading_order()
        {
            string Code(string name) => RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == name));
            var sheet = Code("SettingsControl.Sheet.cs");
            Assert.DoesNotContain("DockPanel.SetDock(foot", sheet);
            var head = sheet.IndexOf("sheet.Children.Add(head);", StringComparison.Ordinal);
            var scroll = sheet.IndexOf("sheet.Children.Add(scroll);", StringComparison.Ordinal);
            var foot = sheet.IndexOf("sheet.Children.Add(foot);", StringComparison.Ordinal);
            Assert.True(head >= 0 && head < scroll && scroll < foot, "the sheet adds its title, then its body, then its footer");
            Assert.Contains("bodyHost.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))", sheet);
            Assert.Contains("Focusable = false,", sheet);
            // The dim closes on a release over itself and not over the sheet.
            Assert.Contains("if (!Within(args.GetPosition(dim), dim) || Within(args.GetPosition(sheetPanel), sheetPanel)) return;", sheet);

            var sidebar = Code("SettingsControl.Sidebar.cs");
            Assert.DoesNotContain("DockPanel.SetDock(foot", sidebar);
            Assert.True(sidebar.IndexOf("Content = top,", StringComparison.Ordinal) < sidebar.IndexOf("dock.Children.Add(foot);", StringComparison.Ordinal), "the sidebar adds its items before its foot");

            var shell = Code("SettingsControl.cs");
            var mainScroll = shell.Substring(shell.IndexOf("private readonly ScrollViewer mainScroll", StringComparison.Ordinal));
            Assert.Contains("Focusable = false,", mainScroll.Substring(0, mainScroll.IndexOf("};", StringComparison.Ordinal)));
            Assert.Contains("new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false, IsTabStop = false }", shell);
        }

        /// <summary>
        /// The shell builds the first page once, at the control's first real width, and a build that throws
        /// lets go of the preview it had started.
        /// </summary>
        [Fact]
        public void The_first_page_is_built_once_and_a_failed_build_drops_its_preview()
        {
            var shell = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.cs"));
            var constructor = shell.Substring(shell.IndexOf("public SettingsControl(OpenDash plugin)", StringComparison.Ordinal));
            constructor = constructor.Substring(0, constructor.IndexOf("private UIElement BuildFrame()", StringComparison.Ordinal));
            Assert.DoesNotContain("Go(route);", constructor);
            Assert.Contains("if (!pageBuilt) BuildFirstPage();", constructor);
            var resize = shell.Substring(shell.IndexOf("private void Resize(double width)", StringComparison.Ordinal));
            Assert.Contains("BuildFirstPage();", resize.Substring(0, resize.IndexOf("if (flipped)", StringComparison.Ordinal)));
            var failure = shell.Substring(shell.IndexOf("catch (Exception ex)", shell.IndexOf("private FrameworkElement BuildPage(", StringComparison.Ordinal), StringComparison.Ordinal));
            Assert.Contains("DropPreview();", failure.Substring(0, failure.IndexOf("return Ui.VStack(", StringComparison.Ordinal)));
        }

        [Fact]
        public void A_sheet_lies_against_the_right_edge_of_the_column_which_is_the_controls()
        {
            // The column fills the control, so the sheet docks right with nothing standing it in, at 4K too.
            Assert.Null(typeof(PanelShell).GetMethod("SheetRightGap"));
            Assert.Equal(560, PanelShell.SheetWidth(3840));
            var sheet = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.Sheet.cs"));
            Assert.Contains("sheetPanel.HorizontalAlignment = HorizontalAlignment.Right;", sheet);
            Assert.DoesNotContain("sheetPanel.Margin", sheet);
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
            // A crumb longer than its row wraps inside its box (#523), so the box takes a little air above and
            // below that a single line, centred in 22, never uses: 12 px words and 3 + 3 still fit 22.
            Assert.Equal(3, PanelShell.CrumbPaddingY);
            Assert.True(PanelShell.CrumbTextSize * 1.33 + 2 * PanelShell.CrumbPaddingY <= PanelShell.CrumbHeight);
            Assert.Equal(12, PanelShell.CrumbTextSize);
            Assert.Equal(13, PanelShell.MessageTextSize);
            // .inp: 30 high, 10 in, 13 px; .num-in: 64 by 30, 8 in, 15 px.
            Assert.Equal(30, PanelShell.InputHeight);
            Assert.Equal(10, PanelShell.InputPaddingX);
            Assert.Equal(13, PanelShell.InputTextSize);
            Assert.Equal(64, PanelShell.NumberInputWidth);
            Assert.Equal(8, PanelShell.NumberInputPaddingX);
            Assert.Equal(15, PanelShell.NumberInputTextSize);
            // The room two columns need. The main column has no ceiling; the artboards' 1200 is a frame.
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

        /// <summary>The merge left controls nobody called -- the shell's old builders, the Install tab's rows and
        /// pills, the drop button, the kit's ToggleRow and SegmentedRow, Profiles' ReinstallBar -- and they are
        /// gone: a helper with no caller is a second way to draw a thing that drifts from the first unseen.</summary>
        [Fact]
        public void The_panel_keeps_no_control_nothing_calls()
        {
            var root = System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            var code = string.Join("\n", System.IO.Directory.GetFiles(root, "*.cs").Select(RepoPaths.Code));
            foreach (var gone in new[]
            {
                "BuildChoice(", "BuildPageSelect(", "BuildPercentBox(", "BuildNumberBox(", "BuildNameBox(", "BuildPrimaryButton(",
                "BuildDestructiveButton(", "BuildTextButton(", "InstallRow(", "StatusPill(", "DropButton(", "SetDropText(", "TryStyle(",
                "RefreshIcon", "ToggleRow(", "SegmentedRow(", "ReinstallBar(",
            })
            {
                Assert.False(Regex.IsMatch(code, @"\b" + Regex.Escape(gone)), gone + " is back");
            }
        }
    }
}
