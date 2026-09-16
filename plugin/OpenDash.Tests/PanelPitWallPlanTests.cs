// PanelPitWallPlanTests.cs: the three page miniatures the Rig tab draws, held against the canvas the way
// PanelFacePlanTests holds the face's own picture.
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

        /// <summary>The miniature itself: 1920 x 1080 at about 0.132, drawn 4 in from every edge, with its
        /// page's name under it at the caption size.</summary>
        [Fact]
        public void A_miniature_is_the_size_the_canvas_draws()
        {
            Assert.Equal(253, PanelPitWallPlan.ThumbWidth);
            Assert.Equal(142, PanelPitWallPlan.ThumbHeight);
            Assert.Equal(16, PanelPitWallPlan.ThumbGap);
            Assert.Equal(4, PanelPitWallPlan.Inset);
            Assert.Equal(8, PanelPitWallPlan.CaptionGap);
            Assert.Equal(Theme.SizeSmall, PanelPitWallPlan.CaptionSize);
            Assert.Equal(0.132, Math.Round(PanelPitWallPlan.ThumbWidth / 1920, 3));
            // The height follows from the width rather than being a scale of its own, so that the
            // miniature is the pit wall's own 16 by 9 and not a rectangle near it.
            Assert.Equal(PanelPitWallPlan.ThumbHeight, Math.Floor(1080 * PanelPitWallPlan.ThumbWidth / 1920));
        }

        /// <summary>Three miniatures, their frames and the two gaps between them, against the 896 a pane's
        /// body is given. The row grew by half when the miniatures did, so this is what says it still fits.</summary>
        [Fact]
        public void The_row_of_three_fits_the_body_of_a_pane()
        {
            var width = 3 * (PanelPitWallPlan.ThumbWidth + 2 * PanelMetrics.BorderWeight) + 2 * PanelPitWallPlan.ThumbGap;
            Assert.Equal(797, width);
            Assert.True(width <= 896, "the row of three is " + width + " wide and the body is 896");
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
            At(Panel("Tower", "C"), 118, 67, 63, 71);
            At(Panel("Tower", "D"), 185, 67, 63, 71);
            Assert.False(Panel("Tower", "Tower").Configurable);
            Assert.True(Panel("Tower", "C").Y > Panel("Tower", "Wide").Bottom, "the wide zone is above C, not beside it");
            Assert.Equal(Panel("Tower", "C").Y, Panel("Tower", "D").Y);
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

        /// <summary>The panels a driver can point at a page are exactly the four letters and the wide zone;
        /// the board and the tower are the page and carry no setting.</summary>
        [Fact]
        public void The_configurable_panels_are_the_zones_the_contract_has()
        {
            var configurable = PanelPitWallPlan.Pages
                .SelectMany(page => page.Panels)
                .Where(panel => panel.Configurable)
                .Select(panel => panel.Name)
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "A", "B", "C", "D", "Wide" }, configurable);
            foreach (var letter in Contract.PitWallZoneLetters)
            {
                Assert.Contains(configurable, name => string.Equals(name, letter, StringComparison.Ordinal));
            }
        }

        /// <summary>The sentences the four rows carry, word for word, because they are what the panel
        /// reads out loud and a rewrite of the table must not quietly rewrite them.</summary>
        [Fact]
        public void A_zone_says_where_it_is_in_the_words_the_canvas_uses()
        {
            Assert.Equal("Race page, upper right. Telemetry page, top.", PanelPitWallPlan.ZoneDescription("A"));
            Assert.Equal("Race page, lower right. Telemetry page, middle.", PanelPitWallPlan.ZoneDescription("B"));
            Assert.Equal("Tower page, lower left. Telemetry page, bottom.", PanelPitWallPlan.ZoneDescription("C"));
            Assert.Equal("Tower page, lower right.", PanelPitWallPlan.ZoneDescription("D"));
        }

        /// <summary>
        /// Every sentence is true of the rectangle the picture draws, and every rectangle has a sentence.
        /// </summary>
        /// <remarks>
        /// The words are checked rather than generated, because "lower left" is read against the zones
        /// sharing a band where there are several and against the middle of the page where a zone is alone
        /// in its band, and no phrasing rule short of that produces the four sentences the canvas writes.
        /// What matters is that a picture the words no longer describe cannot be committed.
        /// </remarks>
        [Fact]
        public void Every_zone_is_drawn_where_its_row_says_it_is()
        {
            foreach (var letter in Contract.PitWallZoneLetters)
            {
                var places = PanelPitWallPlan.PlacesOf(letter);
                Assert.NotEmpty(places);

                var drawnOn = PanelPitWallPlan.Pages
                    .Where(page => page.Panels.Any(panel => string.Equals(panel.Name, letter, StringComparison.Ordinal)))
                    .Select(page => page.Title)
                    .ToArray();
                Assert.Equal(drawnOn, places.Select(place => place.Page).ToArray());

                foreach (var place in places) AssertWhere(letter, place);
            }
        }

        private static void AssertWhere(string letter, PanelPitWallPlan.ZonePlace place)
        {
            var page = Page(place.Page);
            var panel = Panel(place.Page, letter);
            var zones = page.Panels.Where(p => p.Configurable).ToList();
            var said = place.Page + " page, " + place.Where + ": " + letter;

            if (place.Where == "top" || place.Where == "middle" || place.Where == "bottom")
            {
                var order = zones.OrderBy(p => p.Y).Select(p => p.Name).ToList();
                var expected = new List<string> { "top", "middle", "bottom" }[order.IndexOf(letter)];
                Assert.True(place.Where == expected, said + " is the " + expected + " band of three");
                return;
            }

            if (place.Where.StartsWith("upper", StringComparison.Ordinal))
            {
                Assert.True(panel.CentreY < PanelPitWallPlan.ThumbHeight / 2, said + " is drawn in the lower half");
            }
            else
            {
                Assert.True(panel.CentreY > PanelPitWallPlan.ThumbHeight / 2, said + " is drawn in the upper half");
            }

            // Beside another zone, left and right are which of the two is which; alone in its band, they
            // are which side of the page it is on. C above D rather than beside it fails here, because a
            // zone alone in its band at x 118 sits right of the middle whatever the words say.
            var band = zones.Where(p => p.Y < panel.Bottom && panel.Y < p.Bottom).ToList();
            var left = place.Where.EndsWith("left", StringComparison.Ordinal);
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

        /// <summary>The six controls of the list under the picture, at the size the canvas draws them.</summary>
        [Fact]
        public void The_zone_list_is_the_size_the_canvas_draws()
        {
            Assert.Equal(14, PanelPitWallPlan.RowGap);
            Assert.Equal(220, PanelPitWallPlan.SelectWidth);
            Assert.Equal(220, PanelPitWallPlan.AddressWidth);
            Assert.Equal(Theme.ControlHeight, PanelPitWallPlan.SelectHeight);
            Assert.Equal(32, PanelPitWallPlan.SelectHeight);
        }

        /// <summary>A watermark and not a value: what the empty box shows is exactly what
        /// Contract.NormaliseUrl would refuse, which is why it is drawn rather than stored.</summary>
        [Fact]
        public void The_address_watermark_is_not_something_the_contract_would_keep()
        {
            Assert.Equal("https://", PanelPitWallPlan.AddressPlaceholder);
            Assert.Equal(Contract.DefaultWebViewUrl, Contract.NormaliseUrl(PanelPitWallPlan.AddressPlaceholder));
        }

        /// <summary>The companion's grid: two columns, and the two gaps between its rows.</summary>
        [Fact]
        public void The_companion_grid_is_two_columns()
        {
            Assert.Equal(2, PanelCompanionPlan.ModuleColumns);
            Assert.Equal(12, PanelCompanionPlan.ModuleRowGap);
            Assert.Equal(40, PanelCompanionPlan.ModuleColumnGap);
            Assert.Equal(11, (Modules.Count + PanelCompanionPlan.ModuleColumns - 1) / PanelCompanionPlan.ModuleColumns);
        }

        /// <summary>The one action the companion's own section binds, which the contract has to carry for
        /// the row to be bindable at all.</summary>
        [Fact]
        public void A_companion_has_one_wheel_action_to_bind()
        {
            var actions = Contract.ScreenActionNames(Contract.KindCompanion, "Companion").ToArray();
            Assert.Contains(Contract.NextModuleActionFor("Companion"), actions);
        }
    }
}
