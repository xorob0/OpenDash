// PanelPitWallPlanTests.cs: the pit wall picture the Screens page draws -- one page at a time, the one "Page
// on screen" picks -- held against the canvas the way PanelFacePlanTests holds the face's own picture.
//
// The picture and the sentences beside it disagreed before PanelPitWallPlan existed: the Tower page
// stacked C above D and drew the wide zone down the left, while the row beside it read "Tower page, lower
// left" for C and "lower right" for D, and the page's fixed panel was missing altogether. The last test
// below is the one that keeps that from coming back: it reads each sentence's own words off the rectangle
// the same table gives the zone, so a panel that moves either takes its sentence with it or fails here.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelPitWallPlanTests
    {
        private static PanelPitWallPlan.Page Page(string title)
        {
            var page = PanelPitWallPlan.PageNamed(title);
            Assert.True(page != null, "there is no page called " + title);
            return page;
        }

        private static PanelPitWallPlan.Panel Panel(string title, string name)
        {
            var panel = Page(title).Panels.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            Assert.True(panel != null, "the " + title + " page draws no panel called " + name);
            return panel;
        }

        private static void At(PanelPitWallPlan.Panel panel, double x, double y, double width, double height)
        {
            Assert.Equal(new[] { x, y, width, height }, new[] { panel.X, panel.Y, panel.Width, panel.Height });
        }

        /// <summary>The page table's design space: 1920 x 1080 at about 0.132, drawn 4 in from every edge.</summary>
        [Fact]
        public void The_table_is_the_pit_walls_own_sixteen_by_nine()
        {
            Assert.Equal(253, PanelPitWallPlan.ThumbWidth);
            Assert.Equal(142, PanelPitWallPlan.ThumbHeight);
            Assert.Equal(4, PanelPitWallPlan.Inset);
            Assert.Equal(0.132, Math.Round(PanelPitWallPlan.ThumbWidth / 1920, 3));
            // The height follows from the width rather than being a scale of its own, so that the
            // picture is the pit wall's own 16 by 9 and not a rectangle near it.
            Assert.Equal(PanelPitWallPlan.ThumbHeight, Math.Floor(1080 * PanelPitWallPlan.ThumbWidth / 1920));
            Assert.Equal(PanelPitWallPlan.ThumbHeight, PanelPitWallPlan.PictureHeight(PanelPitWallPlan.ThumbWidth));
            Assert.Equal(Math.Floor(1080 * 448.0 / 1920), PanelPitWallPlan.PictureHeight(448));
        }

        /// <summary>The page on screen is drawn as wide as its column: the table scaled, every panel inside
        /// the picture and in the same place relative to the others.</summary>
        [Fact]
        public void A_page_is_the_table_scaled_to_the_width()
        {
            foreach (var page in PanelPitWallPlan.Pages)
            {
                Assert.Equal(page.Panels.Select(p => p.X), PanelPitWallPlan.Scaled(page, PanelPitWallPlan.ThumbWidth).Select(p => p.X));
                foreach (var width in new double[] { 300, 448, 772 })
                {
                    var scaled = PanelPitWallPlan.Scaled(page, width);
                    Assert.Equal(page.Panels.Count, scaled.Count);
                    foreach (var panel in scaled)
                    {
                        Assert.True(panel.X >= 0 && panel.Right <= width, page.Title + " " + panel.Name + " leaves the picture at " + width);
                        Assert.True(panel.Y >= 0 && panel.Bottom <= PanelPitWallPlan.PictureHeight(width), page.Title + " " + panel.Name + " leaves the picture at " + width);
                    }
                }
            }
            Assert.Empty(PanelPitWallPlan.Scaled(null, 300));
            // Beside the picture: the zone list's card, 24 away, as the artboard draws it.
            Assert.Equal(300, PanelPitWallPlan.ListWidth);
            Assert.Equal(24, PanelPitWallPlan.ListGap);
        }

        /// <summary>The picture takes its column, frame included: beside the list the column is the content
        /// less the list and the gap, stacked it is the content, never below the least; the page inside is the
        /// column less the frame, so the rule is not drawn 2 px past the column and clipped.</summary>
        [Fact]
        public void The_picture_and_its_frame_fit_the_column()
        {
            Assert.Equal(PanelShell.ContentMax - 324, PanelPitWallPlan.PictureWidthFor(PanelShell.ContentMax, true));
            Assert.Equal(544, PanelPitWallPlan.PictureWidthFor(544, false));
            // Stacked, the picture stops where its page would stand taller than the face's picture may, so
            // the zone list and the rows under it stay in sight on a wide page.
            Assert.Equal(750, PanelPitWallPlan.StackedMax);
            Assert.Equal(PanelPitWallPlan.StackedMax, PanelPitWallPlan.PictureWidthFor(999, false));
            Assert.Equal(PanelPitWallPlan.StackedMax, PanelPitWallPlan.PictureWidthFor(PanelShell.ContentMax, false));
            Assert.True(PanelPitWallPlan.PictureHeight(PanelPitWallPlan.CanvasWidth(PanelPitWallPlan.StackedMax)) <= PanelFacePlan.MaxHeight);
            Assert.True(PanelPitWallPlan.PictureHeight(PanelPitWallPlan.CanvasWidth(PanelPitWallPlan.StackedMax + 1)) > PanelFacePlan.MaxHeight);
            Assert.Equal(PanelPitWallPlan.PictureLeast, PanelPitWallPlan.PictureWidthFor(300, true));
            Assert.Equal(120, PanelPitWallPlan.PictureLeast);
            Assert.Equal(1, PanelPitWallPlan.Frame);
            Assert.Equal(542, PanelPitWallPlan.CanvasWidth(544));
            Assert.Equal(PanelPitWallPlan.PictureWidthFor(900, true), PanelPitWallPlan.CanvasWidth(PanelPitWallPlan.PictureWidthFor(900, true)) + 2 * PanelPitWallPlan.Frame);
        }

        /// <summary>The three pages, in the order the picture draws them.</summary>
        [Fact]
        public void The_three_pages_are_the_ones_the_pit_wall_has()
        {
            Assert.Equal(new[] { "Race", "Tower", "Telemetry" }, PanelPitWallPlan.Pages.Select(p => p.Title).ToArray());
        }

        /// <summary>Race: the fixed board down the left, zone A upper right and zone B under it.</summary>
        [Fact]
        public void The_race_page_is_a_board_and_two_zones()
        {
            Assert.Equal(3, Page("Race").Panels.Count);
            At(Panel("Race", "Board"), 4, 4, 118, 134);
            At(Panel("Race", "A"), 128, 4, 118, 65);
            At(Panel("Race", "B"), 128, 73, 118, 65);
            Assert.False(Panel("Race", "Board").Configurable);
        }

        /// <summary>
        /// Tower: the fixed tower down the left, the wide zone across the top of the right half, and C
        /// beside D under it.
        /// </summary>
        /// <remarks>
        /// Four panels, which the two-column helper this replaced could not draw: it gave the page three,
        /// put the wide zone where the tower belongs and stacked C above D. The count alone fails on that
        /// version, which is why it is asserted before the rectangles.
        /// </remarks>
        [Fact]
        public void The_tower_page_is_a_tower_a_wide_zone_and_two_zones_side_by_side()
        {
            Assert.Equal(4, Page("Tower").Panels.Count);
            At(Panel("Tower", "Tower"), 4, 4, 110, 134);
            At(Panel("Tower", "Wide"), 118, 4, 131, 59);
            At(Panel("Tower", "A"), 118, 67, 63, 71);
            At(Panel("Tower", "B"), 185, 67, 63, 71);
            Assert.False(Panel("Tower", "Tower").Configurable);
            Assert.True(Panel("Tower", "A").Y > Panel("Tower", "Wide").Bottom, "the wide zone is above the pair, not beside it");
            Assert.Equal(Panel("Tower", "A").Y, Panel("Tower", "B").Y);
        }

        /// <summary>Telemetry: three full-width bands, the middle one two pixels taller than the other two.</summary>
        [Fact]
        public void The_telemetry_page_is_three_stacked_bands()
        {
            Assert.Equal(3, Page("Telemetry").Panels.Count);
            At(Panel("Telemetry", "A"), 4, 4, 243, 39);
            At(Panel("Telemetry", "B"), 4, 49, 243, 41);
            At(Panel("Telemetry", "C"), 4, 96, 243, 39);
            Assert.True(Page("Telemetry").Panels.All(p => p.Configurable), "every band of the telemetry page is a zone");
        }

        /// <summary>Nothing is drawn outside the miniature or over its neighbour, at any inset.</summary>
        [Fact]
        public void No_panel_leaves_its_page_or_covers_another()
        {
            foreach (var page in PanelPitWallPlan.Pages)
            {
                foreach (var panel in page.Panels)
                {
                    Assert.True(panel.X >= PanelPitWallPlan.Inset && panel.Y >= PanelPitWallPlan.Inset,
                        page.Title + "'s " + panel.Name + " starts at " + panel.X + "," + panel.Y);
                    Assert.True(panel.Right <= PanelPitWallPlan.ThumbWidth - PanelPitWallPlan.Inset,
                        page.Title + "'s " + panel.Name + " reaches " + panel.Right);
                    Assert.True(panel.Bottom <= PanelPitWallPlan.ThumbHeight - PanelPitWallPlan.Inset,
                        page.Title + "'s " + panel.Name + " reaches " + panel.Bottom);
                }
                for (var i = 0; i < page.Panels.Count; i++)
                {
                    for (var j = i + 1; j < page.Panels.Count; j++)
                    {
                        var a = page.Panels[i];
                        var b = page.Panels[j];
                        var overlaps = a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
                        Assert.False(overlaps, page.Title + " draws " + a.Name + " over " + b.Name);
                    }
                }
            }
        }

        /// <summary>
        /// The panels a driver can point at are exactly the landscape zones the contract has, page for page.
        /// </summary>
        /// <remarks>
        /// Page and slot together, because a zone belongs to a page: the picture draws an A on all three
        /// pages and they are three different settings. Checked both ways, so a rectangle the contract does
        /// not name and a setting the picture does not draw both fail here.
        /// </remarks>
        [Fact]
        public void The_configurable_panels_are_the_zones_the_contract_has()
        {
            var drawn = PanelPitWallPlan.Pages
                .SelectMany(page => page.Panels.Where(panel => panel.Configurable).Select(panel => page.Title + panel.Name))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            var declared = Contract.PitWallZoneSlots
                .Where(slot => slot.Landscape)
                .Select(slot => slot.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(declared, drawn);
        }

        /// <summary>The sentence each row carries, word for word, because it is what the panel reads out
        /// loud and a rewrite of the table must not quietly rewrite it.</summary>
        [Fact]
        public void A_zone_says_where_it_is_in_the_words_the_canvas_uses()
        {
            Assert.Equal("Race page, upper right.", Description("RaceA"));
            Assert.Equal("Race page, lower right.", Description("RaceB"));
            Assert.Equal("Tower page, lower left.", Description("TowerA"));
            Assert.Equal("Tower page, lower right.", Description("TowerB"));
            Assert.Equal("Telemetry page, top.", Description("TelemetryA"));
            // A portrait zone's hover is its place alone, under a row that already says Portrait layout.
            Assert.Equal("Upper left.", PanelPitWallPlan.ZonePosition(Contract.PitWallZoneSlotByKey("PortraitA")));
            Assert.Equal("Lower right.", PanelPitWallPlan.ZonePosition(Contract.PitWallZoneSlotByKey("PortraitD")));
            Assert.Equal(string.Empty, PanelPitWallPlan.ZonePosition(null));
            // Every zone of every page has one, the portrait package's included.
            foreach (var slot in Contract.PitWallZoneSlots) Assert.NotEqual(string.Empty, PanelPitWallPlan.ZoneDescription(slot));
        }

        private static string Description(string key)
        {
            return PanelPitWallPlan.ZoneDescription(Contract.PitWallZoneSlotByKey(key));
        }

        /// <summary>
        /// Every sentence is true of the rectangle the picture draws.
        /// </summary>
        /// <remarks>
        /// The words are checked rather than generated, because "lower left" is read against the zones
        /// sharing a band where there are several and against the middle of the page where a zone is alone
        /// in its band, and no phrasing rule short of that produces the sentences the canvas writes. What
        /// matters is that a picture the words no longer describe cannot be committed.
        /// </remarks>
        [Fact]
        public void Every_zone_is_drawn_where_its_row_says_it_is()
        {
            foreach (var slot in Contract.PitWallZoneSlots)
            {
                if (!slot.Landscape) continue;
                var sentence = PanelPitWallPlan.ZoneDescription(slot);
                Assert.StartsWith(slot.Page + " page, ", sentence, StringComparison.Ordinal);
                var where = sentence.Substring((slot.Page + " page, ").Length).TrimEnd('.');
                // The wide zone spans its column and is described rather than placed, so there is nothing
                // left and right to check it against.
                if (slot.Wide) continue;
                AssertWhere(slot, where);
            }
        }

        private static void AssertWhere(Contract.PitWallZoneSlot slot, string where)
        {
            var letter = slot.Slot;
            var page = Page(slot.Page);
            var panel = Panel(slot.Page, letter);
            var zones = page.Panels.Where(p => p.Configurable).ToList();
            var said = slot.Page + " page, " + where + ": " + letter;

            if (where == "top" || where == "middle" || where == "bottom")
            {
                var order = zones.OrderBy(p => p.Y).Select(p => p.Name).ToList();
                var expected = new List<string> { "top", "middle", "bottom" }[order.IndexOf(letter)];
                Assert.True(where == expected, said + " is the " + expected + " band of three");
                return;
            }

            if (where.StartsWith("upper", StringComparison.Ordinal))
            {
                Assert.True(panel.CentreY < PanelPitWallPlan.ThumbHeight / 2, said + " is drawn in the lower half");
            }
            else
            {
                Assert.True(panel.CentreY > PanelPitWallPlan.ThumbHeight / 2, said + " is drawn in the upper half");
            }

            // Beside another zone, left and right are which of the two is which; alone in its band, they
            // are which side of the page it is on. One above the other rather than beside it fails here,
            // because a zone alone in its band at x 118 sits right of the middle whatever the words say.
            var band = zones.Where(p => p.Y < panel.Bottom && panel.Y < p.Bottom).ToList();
            var left = where.EndsWith("left", StringComparison.Ordinal);
            if (band.Count > 1)
            {
                var edge = left ? band.Min(p => p.X) : band.Max(p => p.X);
                Assert.True(panel.X == edge, said + " is not the " + (left ? "left" : "right") + " of its band");
            }
            else if (left)
            {
                Assert.True(panel.CentreX < PanelPitWallPlan.ThumbWidth / 2, said + " is drawn right of the middle");
            }
            else
            {
                Assert.True(panel.CentreX > PanelPitWallPlan.ThumbWidth / 2, said + " is drawn left of the middle");
            }
        }

        /// <summary>The web view address box is the artboard's 320, and narrower only where 320 would squeeze
        /// its row's title under the least every other row keeps.</summary>
        [Fact]
        public void The_address_box_is_the_width_the_canvas_draws()
        {
            Assert.Equal(320, PanelPitWallPlan.AddressWidth);
            Assert.Equal(320, PanelPitWallPlan.AddressWidthFor(PanelShell.ContentMax));
            Assert.Equal(320, PanelPitWallPlan.AddressWidthFor(320 + PanelShell.RowGap + PanelScreens.RowTitleLeast));
            Assert.Equal(PanelScreens.ControlsWidth(400), PanelPitWallPlan.AddressWidthFor(400));
            Assert.True(PanelPitWallPlan.AddressWidthFor(400) < 320);
        }

        /// <summary>The fixed panels say what they show under their name, as the artboard's Race picture writes
        /// "Board · Leaderboard"; a zone's page is its setting and the table leaves it empty.</summary>
        [Fact]
        public void A_fixed_panel_says_what_it_shows()
        {
            Assert.Equal("Leaderboard", Panel("Race", "Board").Shows);
            Assert.Equal("Leaderboard", Panel("Tower", "Tower").Shows);
            foreach (var page in PanelPitWallPlan.Pages)
            {
                foreach (var panel in page.Panels)
                {
                    Assert.Equal(panel.Configurable, panel.Shows == null);
                }
                Assert.Equal(page.Panels.Select(p => p.Shows), PanelPitWallPlan.Scaled(page, 448).Select(p => p.Shows));
            }
        }

        /// <summary>A watermark and not a value: what the empty box shows is exactly what
        /// Contract.NormaliseUrl would refuse, which is why it is drawn rather than stored.</summary>
        [Fact]
        public void The_address_watermark_is_not_something_the_contract_would_keep()
        {
            Assert.Equal("https://", PanelPitWallPlan.AddressPlaceholder);
            Assert.Equal(Contract.DefaultWebViewUrl, Contract.NormaliseUrl(PanelPitWallPlan.AddressPlaceholder));
        }

        /// <summary>The companion's grid: three columns of seven where they fit (ruling 36), fewer where a
        /// module and "Not in iRacing" beside it would not.</summary>
        [Fact]
        public void The_companion_grid_is_three_columns_where_they_fit()
        {
            Assert.Equal(3, PanelCompanionPlan.ModuleColumns);
            Assert.Equal(7, (Modules.Count + PanelCompanionPlan.ModuleColumns - 1) / PanelCompanionPlan.ModuleColumns);
            Assert.Equal(6, PanelCompanionPlan.ModuleGap);
            Assert.Equal(34, PanelCompanionPlan.ModuleHeight);
            Assert.Equal(10, PanelCompanionPlan.ModulePaddingX);
            Assert.Equal(3, PanelCompanionPlan.ColumnsFor(PanelShell.ContentMax));
            Assert.Equal(3, PanelCompanionPlan.ColumnsFor(612));
            Assert.Equal(2, PanelCompanionPlan.ColumnsFor(611));
            Assert.Equal(2, PanelCompanionPlan.ColumnsFor(406));
            Assert.Equal(1, PanelCompanionPlan.ColumnsFor(405));
            Assert.Equal(1, PanelCompanionPlan.ColumnsFor(0));
            Assert.Equal(11, PanelCompanionPlan.NoteSize);
        }

        /// <summary>The pit wall picture's panels: the name at 12 over the page at 14, padded 8 by 6.</summary>
        [Fact]
        public void A_pit_wall_panel_names_its_zone_over_its_page()
        {
            Assert.Equal(12, PanelPitWallPlan.ZoneNameSize);
            Assert.Equal(14, PanelPitWallPlan.ZonePageSize);
            Assert.Equal(8, PanelPitWallPlan.ZonePaddingX);
            Assert.Equal(6, PanelPitWallPlan.ZonePaddingY);
        }

        /// <summary>
        /// The round screen's disc, as Screens.dc.html draws it: 240 across, 32 from the rows, its cards 6
        /// apart and padded 6 by 7, one column of 140 for two cards and two of 84 for six, every corner of
        /// every card inside the disc; twelve cards are not drawn on it at all.
        /// </summary>
        [Fact]
        public void A_round_screens_picture_is_the_artboards_disc()
        {
            Assert.Equal(240, PanelRoundPlan.PictureSize);
            Assert.Equal(32, PanelRoundPlan.PictureGap);
            Assert.Equal(6, PanelRoundPlan.CardGap);
            Assert.Equal(6, PanelRoundPlan.CardPaddingX);
            Assert.Equal(7, PanelRoundPlan.CardPaddingY);
            Assert.Equal(1, PanelRoundPlan.Columns(2));
            Assert.Equal(2, PanelRoundPlan.Columns(6));
            Assert.Equal(2, PanelRoundPlan.Columns(Contract.SlotCount));
            Assert.Equal(140, PanelRoundPlan.CardWidth(1));
            Assert.Equal(84, PanelRoundPlan.CardWidth(2));
            // A card is its rule, its padding and its two lines at their line height.
            var lines = PanelFacePlan.LineHeight * (PanelShell.EyebrowSize + Theme.SizeBody);
            Assert.Equal(PanelRoundPlan.CardHeight, Math.Ceiling(2 * PanelMetrics.BorderWeight + 2 * PanelRoundPlan.CardPaddingY + lines + PanelRoundPlan.CardLineGap));

            // Every corner of the block of cards, which the picture centres on the disc, lies inside it.
            var radius = PanelRoundPlan.PictureSize / 2;
            foreach (var read in new[] { 2, 6 })
            {
                Assert.True(PanelRoundPlan.OnDisc(read));
                var columns = PanelRoundPlan.Columns(read);
                var rows = (read + columns - 1) / columns;
                var width = columns * PanelRoundPlan.CardWidth(columns) + (columns - 1) * PanelRoundPlan.CardGap;
                var height = rows * PanelRoundPlan.CardHeight + (rows - 1) * PanelRoundPlan.CardGap;
                Assert.True(height < PanelRoundPlan.PictureSize, read + " cards stand " + height + " tall");
                var corner = Math.Sqrt(width * width / 4 + height * height / 4);
                Assert.True(corner <= radius, read + " cards put a corner " + corner + " from the disc's centre");
            }
            // Twelve, on a card face of another size, stand taller than the disc, so the rows are drawn alone.
            Assert.False(PanelRoundPlan.OnDisc(Contract.SlotCount));
            Assert.Equal(PanelRoundPlan.MostOnDisc, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 800, Height = 800 }));
        }

        /// <summary>A companion's section binds one action of OpenDash's, the held glance: its paging is
        /// SimHub's, bound on the device the companion runs on.</summary>
        [Fact]
        public void A_companion_binds_its_glance_and_nothing_to_page_with()
        {
            // SimHub's own NextScreen and PreviousScreen, bound in the Controls and events of the device
            // the companion runs on, are what page it from a button, and a tap on the screen is what pages
            // it from the screen. The glance is OpenDash's (#362). See ContractTests and ScreenActionsTests.
            Assert.Equal(new[] { "CompanionHoldQuickGlance" }, Contract.ScreenActionNames(Contract.KindCompanion, "Companion").ToArray());
        }
    }
}
