// PanelFacePlanTests.cs: the picture the Screens page configures a face on, held against the canvas the way
// PanelMetricsTests holds the rest of the panel's geometry.
//
// SettingsControl.Screens.Face.cs is WPF and cannot be compiled here, which is why PanelFacePlan.cs exists: a
// picture whose rows are four constants rather than the face's own rectangles is caught here rather than
// on the VM, where the only way to see it is to open the panel and measure it.
using System;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelFacePlanTests
    {
        private static Contract.FaceSize Reference { get { return Contract.ReferenceFace; } }

        /// <summary>
        /// The 1920 x 480 face's own proportions at 844 across, the width For(face) takes when nothing says
        /// otherwise: its rows 21, 24, 138 and 36, and its zones 334, 165 and 335 across, 5 apart.
        /// </summary>
        /// <remarks>
        /// Not the width the page draws: Screens.dc.html puts the picture in a 556 px column, and the page in
        /// its column beside the aside (see the next test). 844 is the width the old pane's body gave it, kept
        /// as the default so every face's proportions are held at one width.
        /// </remarks>
        [Fact]
        public void The_reference_face_keeps_its_own_proportions_at_844()
        {
            var plan = PanelFacePlan.For(Reference);
            Assert.Equal(844, PanelFacePlan.PictureWidth);
            Assert.Equal(844, plan.Width);
            Assert.Equal(21, plan.RevBar);
            Assert.Equal(24, plan.Bar);
            Assert.Equal(138, plan.Body);
            Assert.Equal(36, plan.Band);
            Assert.Equal(new[] { "B", "A", "C" }, plan.Letters);
            Assert.Equal(new double[] { 334, 165, 335 }, plan.Cells);
        }

        /// <summary>The picture at another width is the same rule at that width: For(face) is For(face, 844),
        /// and a narrower one scales every row by its own width and keeps each row's least.</summary>
        [Fact]
        public void A_picture_is_drawn_at_the_width_the_page_gives_it()
        {
            foreach (var face in Contract.FaceSizes)
            {
                var standard = PanelFacePlan.For(face);
                var same = PanelFacePlan.For(face, PanelFacePlan.PictureWidth);
                Assert.Equal(standard.Cells, same.Cells);
                Assert.Equal(standard.Height, same.Height);
            }
            // Beside the aside on the widest page (1112 less 316 and 24) and alone on a phone-width one.
            Assert.Equal(772, PanelFacePlan.PictureWidthFor(PanelShell.ContentMax, true));
            Assert.Equal(400, PanelFacePlan.PictureWidthFor(400, false));
            Assert.Equal(754, PanelFacePlan.RowsWidth(772));
            var wide = PanelFacePlan.For(Contract.FaceSizes[1], 754);
            Assert.Equal(754, wide.Cells.Sum() + 2 * PanelFacePlan.Seam);
            Assert.Equal(Math.Floor(320 * 754.0 / 1280), wide.Body);
            var narrow = PanelFacePlan.For(Reference, 300);
            Assert.Equal(PanelFacePlan.CellLeast, narrow.Body);
            Assert.Equal(PanelFacePlan.RevBarLeast, narrow.RevBar);
            Assert.Equal(PanelFacePlan.BandLeast, narrow.Band);
        }

        /// <summary>
        /// The reference face as the page draws it beside the aside on the widest page: a picture 772 wide,
        /// rows 754 across, 18, 24, 123 and 36 high, zones 298, 147 and 299 across.
        /// </summary>
        [Fact]
        public void The_reference_face_is_drawn_beside_the_aside_at_754()
        {
            var outer = PanelFacePlan.FitWidth(Reference, PanelFacePlan.PictureWidthFor(PanelShell.ContentMax, true));
            Assert.Equal(772, outer);
            var plan = PanelFacePlan.For(Reference, PanelFacePlan.RowsWidth(outer));
            Assert.Equal(754, plan.Width);
            Assert.Equal(18, plan.RevBar);
            Assert.Equal(24, plan.Bar);
            Assert.Equal(123, plan.Body);
            Assert.Equal(36, plan.Band);
            Assert.Equal(new double[] { 298, 147, 299 }, plan.Cells);
        }

        /// <summary>
        /// The aside stands beside the picture only while the picture's column is the artboard's 556 (896 less
        /// the aside and the gap): narrower, the rows under the picture had no room for their titles, and the
        /// zone cells fell to their least.
        /// </summary>
        [Fact]
        public void The_aside_stands_beside_the_picture_while_the_column_is_the_artboards()
        {
            Assert.Equal(556, PanelFacePlan.ColumnLeast);
            Assert.Equal(896 - PanelFacePlan.AsideWidth - PanelFacePlan.AsideGap, PanelFacePlan.ColumnLeast);
            Assert.False(PanelFacePlan.SideBySide(PanelShell.TwoColumnFrom, true));
            Assert.False(PanelFacePlan.SideBySide(895, true));
            Assert.True(PanelFacePlan.SideBySide(896, true));
            Assert.True(PanelFacePlan.SideBySide(PanelShell.ContentMax, true));
            Assert.False(PanelFacePlan.SideBySide(PanelShell.ContentMax, false));
            // Beside the aside, the reference face's cells scale past their least at every width.
            var least = PanelFacePlan.For(Reference, PanelFacePlan.RowsWidth(PanelFacePlan.ColumnLeast));
            Assert.True(least.Body > PanelFacePlan.CellLeast, "the reference face's body is " + least.Body + " at the least column");
        }

        /// <summary>
        /// No face's picture stands taller than the cap, however wide its column: the portrait face at the
        /// column's width was 891 px tall beside the aside and pushed the rows off the page. Where a face at
        /// its least is taller than the cap, it is drawn at its least.
        /// </summary>
        [Fact]
        public void A_picture_is_never_taller_than_the_page_can_show()
        {
            var frame = 2 * (PanelFacePlan.Inset + PanelFacePlan.Frame);
            foreach (var face in Contract.FaceSizes)
            {
                foreach (var column in new double[] { 360, 544, 772, 1112 })
                {
                    var outer = PanelFacePlan.FitWidth(face, column);
                    Assert.True(outer <= column, face + " is drawn " + outer + " wide in a column of " + column);
                    var height = PanelFacePlan.For(face, PanelFacePlan.RowsWidth(outer)).Height + frame;
                    var least = PanelFacePlan.For(face, 1).Height + frame;
                    Assert.True(height <= Math.Max(PanelFacePlan.MaxHeight, least), face + " stands " + height + " tall in a column of " + column);
                    if (outer < column)
                    {
                        var wider = PanelFacePlan.For(face, PanelFacePlan.RowsWidth(outer + 1)).Height + frame;
                        Assert.True(wider > Math.Max(PanelFacePlan.MaxHeight, least), face + " could be drawn wider than " + outer);
                    }
                }
            }
            var portrait = Contract.FaceSizes.Single(f => f.Body == Contract.FaceBody.Column);
            Assert.True(PanelFacePlan.FitWidth(portrait, 772) < 772);
            Assert.Equal(420, PanelFacePlan.MaxHeight);
        }

        /// <summary>The picture's frame and the cells' insides, as Screens.dc.html draws them: an 8 px inset
        /// inside a 1 px rule, parts 5 apart, a zone padded 9 by 10 with its lines 6 apart, the accent's 2 px
        /// edge on the part the aside shows, and the aside itself 316 wide, 24 from the picture.</summary>
        [Fact]
        public void The_picture_is_framed_and_its_cells_padded_as_the_canvas_draws_them()
        {
            Assert.Equal(8, PanelFacePlan.Inset);
            Assert.Equal(1, PanelFacePlan.Frame);
            Assert.Equal(5, PanelFacePlan.Seam);
            Assert.Equal(10, PanelFacePlan.CellPaddingX);
            Assert.Equal(9, PanelFacePlan.CellPaddingY);
            Assert.Equal(6, PanelFacePlan.CellGap);
            Assert.Equal(18, PanelFacePlan.LetterSize);
            Assert.Equal(16, PanelFacePlan.BandLetterSize);
            Assert.Equal(13, PanelFacePlan.CountSize);
            Assert.Equal(15, PanelFacePlan.PageSize);
            Assert.Equal(14, PanelFacePlan.BandPageSize);
            Assert.Equal(12, PanelFacePlan.ButtonLineSize);
            Assert.Equal(11, PanelFacePlan.BarTextSize);
            Assert.Equal(2, PanelFacePlan.SelectedEdge);
            Assert.Equal(316, PanelFacePlan.AsideWidth);
            Assert.Equal(24, PanelFacePlan.AsideGap);
            // A binding chip in the aside is cut short where it would take "Previous page" and its NEW tag's
            // 131 px of the aside's 282 inside its rule and padding, 12 apart.
            Assert.Equal(136, PanelFacePlan.AsideChipMax);
            Assert.True(PanelFacePlan.AsideWidth - 2 * PanelMetrics.BorderWeight - 2 * 16 - PanelFacePlan.AsideChipMax - 12 >= 131);
        }

        /// <summary>The rev strip is twenty segments of a mid-range shift, and dark when the rev bar is off.</summary>
        [Fact]
        public void The_rev_strip_is_twenty_segments_that_go_dark_with_the_rev_bar()
        {
            var on = PanelFacePlan.RevColours(true);
            Assert.Equal(20, on.Length);
            Assert.Equal(9, on.Count(c => c == Theme.ShiftStage1));
            Assert.Equal(5, on.Count(c => c == Theme.ShiftStage2));
            Assert.Equal(2, on.Count(c => c == Theme.ShiftStage3));
            Assert.Equal(Theme.ShiftStage1, on[0]);
            Assert.Equal(Theme.SurfaceRaised, on[19]);
            Assert.All(PanelFacePlan.RevColours(false), c => Assert.Equal(Theme.SurfaceRaised, c));
            Assert.Equal(3, PanelFacePlan.RevGap);
        }

        /// <summary>The rows the face's own rectangles are too small to carry: the rev strip's 16, the bar's
        /// short control height, band D's line of four facts, and a zone's three lines.</summary>
        [Fact]
        public void A_row_is_never_smaller_than_what_it_has_to_hold()
        {
            Assert.Equal(16, PanelFacePlan.RevBarLeast);
            Assert.Equal(36, PanelFacePlan.BandLeast);
            Assert.Equal(86, PanelFacePlan.CellLeast);
            Assert.Equal(Theme.ControlHeightSm, PanelFacePlan.BarLeast);
            // A zone's three lines at their line height, the gaps, its padding and its border fit the least:
            // font sizes alone came to 75 and let the page line be squeezed and its descenders cut.
            Assert.Equal(1.2, PanelFacePlan.LineHeight);
            var lines = PanelFacePlan.LineHeight * (PanelFacePlan.LetterSize + PanelFacePlan.PageSize + PanelFacePlan.ButtonLineSize);
            var cell = lines + 2 * PanelFacePlan.CellGap + 2 * PanelFacePlan.CellPaddingY + 2 * PanelMetrics.BorderWeight;
            Assert.Equal(PanelFacePlan.CellLeast, Math.Ceiling(cell));

            foreach (var face in Contract.FaceSizes)
            {
                var plan = PanelFacePlan.For(face);
                Assert.True(plan.RevBar >= PanelFacePlan.RevBarLeast, face + " draws a rev bar of " + plan.RevBar);
                Assert.True(plan.Band >= PanelFacePlan.BandLeast, face + " draws band D at " + plan.Band);
                if (plan.HasBar) Assert.True(plan.Bar >= PanelFacePlan.BarLeast, face + " draws a bar of " + plan.Bar);
            }
        }

        /// <summary>
        /// A row is the face's own rectangle at the picture's scale, and grows past it only where it has
        /// to hold a control. That is the whole of the rule, and it is what the four constants the plan
        /// replaced could not do.
        /// </summary>
        [Fact]
        public void A_row_is_the_faces_own_rectangle_scaled_to_the_picture()
        {
            foreach (var face in Contract.FaceSizes)
            {
                var plan = PanelFacePlan.For(face);
                var scale = PanelFacePlan.PictureWidth / face.Width;
                AssertScaled(face.RevBarHeight, scale, PanelFacePlan.RevBarLeast, plan.RevBar, face + " rev bar");
                if (plan.HasBar) AssertScaled(face.BarHeight, scale, PanelFacePlan.BarLeast, plan.Bar, face + " bar");
                AssertScaled(face.BandHeight, scale, PanelFacePlan.BandLeast, plan.Band, face + " band D");
            }
        }

        private static void AssertScaled(int rect, double scale, double least, double drawn, string what)
        {
            var scaled = Math.Floor(rect * scale);
            Assert.True(drawn == scaled || (scaled < least && drawn == least),
                what + " is drawn " + drawn + " where the face scales to " + scaled + " and its least is " + least);
        }

        /// <summary>The cells and the gaps between them fill the body exactly: a cell rounded on its own
        /// would leave a sliver of ground showing at the end of the row.</summary>
        [Fact]
        public void The_cells_and_their_seams_fill_the_body_exactly()
        {
            foreach (var face in Contract.FaceSizes)
            {
                var plan = PanelFacePlan.For(face);
                var span = plan.Stacked ? plan.Body : plan.Width;
                var seams = (plan.Cells.Length - 1) * PanelFacePlan.Seam;
                Assert.Equal(span, plan.Cells.Sum() + seams);
            }
        }

        /// <summary>The nano has no bar, so its picture has three rows and not four.</summary>
        [Fact]
        public void A_face_without_a_bar_is_drawn_without_the_row()
        {
            var nano = Contract.FaceSizes.Single(f => !f.HasBar);
            var plan = PanelFacePlan.For(nano);
            Assert.Equal(0, plan.Bar);
            Assert.Equal(plan.RevBar + PanelFacePlan.Seam + plan.Body + PanelFacePlan.Seam + plan.Band, plan.Height);
        }

        /// <summary>The portrait stacks its zones, so each of the three has to be a cell in its own right
        /// rather than a third of one.</summary>
        [Fact]
        public void A_portrait_face_stacks_three_cells_that_each_hold_their_controls()
        {
            var portrait = Contract.FaceSizes.Single(f => f.Body == Contract.FaceBody.Column);
            var plan = PanelFacePlan.For(portrait);
            Assert.True(plan.Stacked);
            Assert.Equal(new[] { "A", "B", "C" }, plan.Letters);
            foreach (var cell in plan.Cells)
            {
                Assert.True(cell >= PanelFacePlan.CellLeast, portrait + " stacks a cell of " + cell);
            }
        }

        /// <summary>
        /// The panel reads the zones in the order the picture draws them, band D last. The settings are
        /// indexed by Contract.FaceZoneLetters and keep their own order, so the two must hold the same
        /// four letters: a letter dropped from the panel is a setting nobody can reach.
        /// </summary>
        [Fact]
        public void The_display_order_is_a_permutation_of_the_settings_order()
        {
            Assert.Equal(new[] { "B", "A", "C", "D" }, PanelFacePlan.ZoneOrder(Reference));
            foreach (var face in Contract.FaceSizes)
            {
                var order = PanelFacePlan.ZoneOrder(face);
                Assert.Equal(Contract.FaceZoneLetters.OrderBy(l => l, StringComparer.Ordinal).ToArray(),
                    order.OrderBy(l => l, StringComparer.Ordinal).ToArray());
                Assert.Equal("D", order[order.Length - 1]);
            }
        }

        /// <summary>Band D is a zone everywhere but on the panel, where it is called what it looks like.</summary>
        [Fact]
        public void Band_D_is_labelled_a_band_and_keyed_as_a_zone()
        {
            Assert.Equal("Band D", PanelFacePlan.ZoneLabel("D"));
            Assert.Equal("Zone B", PanelFacePlan.ZoneLabel("B"));
            Assert.Contains("D", Contract.FaceZoneLetters);
        }

        /// <summary>The glance is one choice: its control names the zone and the page together, and the
        /// default reads the way the canvas draws it.</summary>
        [Fact]
        public void The_glance_names_the_zone_and_the_page_together()
        {
            Assert.Equal("Zone C · Track", PanelFacePlan.GlanceLabel(Contract.DefaultQuickGlance));
            Assert.Equal("Zone A · Gear, speed, revs", PanelFacePlan.GlanceLabel(Contract.QuickGlanceValue(0, 0)));
            Assert.Equal("Band D · Fuel", PanelFacePlan.GlanceLabel(Contract.QuickGlanceValue(3, 0)));
        }

        /// <summary>One entry per page of every zone, each of them a value the settings accept as it
        /// stands, so that what the select writes is never normalised out from under it.</summary>
        [Fact]
        public void The_glance_offers_every_page_of_every_zone()
        {
            var options = PanelFacePlan.GlanceOptions();
            var pages = Enumerable.Range(0, Contract.FaceZoneLetters.Length).Sum(FacePages.CountAt);
            Assert.Equal(pages, options.Length);
            Assert.Equal(options.Length, options.Distinct().Count());
            Assert.Contains(Contract.DefaultQuickGlance, options);
            foreach (var option in options) Assert.Equal(option, Contract.NormaliseQuickGlance(option));
        }
    }
}
