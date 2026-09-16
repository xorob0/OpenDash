// PanelFacePlanTests.cs: the picture the Rig tab configures a face on, held against the canvas the way
// PanelMetricsTests holds the rest of the panel's geometry.
//
// SettingsControl.Panes.cs is WPF and cannot be compiled here, which is why PanelFacePlan.cs exists: a
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

        /// <summary>The plan of the 1920 x 480 face, which is the one the canvas draws: 844 wide, its rows
        /// 26, 24, 138 and 30, and its zones 338, 167 and 337 across.</summary>
        [Fact]
        public void The_reference_face_is_the_picture_the_canvas_draws()
        {
            var plan = PanelFacePlan.For(Reference);
            Assert.Equal(844, PanelFacePlan.PictureWidth);
            Assert.Equal(26, plan.RevBar);
            Assert.Equal(24, plan.Bar);
            Assert.Equal(138, plan.Body);
            Assert.Equal(30, plan.Band);
            Assert.Equal(new[] { "B", "A", "C" }, plan.Letters);
            Assert.Equal(new double[] { 338, 167, 337 }, plan.Cells);
        }

        /// <summary>What a cell gives the two controls in it: 324, 153 and 323, which is the cell less the
        /// seven of padding at each side.</summary>
        [Fact]
        public void A_cell_gives_its_controls_the_width_the_canvas_draws()
        {
            var plan = PanelFacePlan.For(Reference);
            Assert.Equal(new double[] { 324, 153, 323 }, plan.Cells.Select(PanelFacePlan.Inner).ToArray());
            Assert.Equal(7, PanelFacePlan.CellPaddingX);
            Assert.Equal(8, PanelFacePlan.CellPaddingY);
            Assert.Equal(9, PanelFacePlan.CellGap);
        }

        /// <summary>The controls the picture carries, at the sizes the canvas draws them: the two ends of
        /// the bar, band D's row, the count of a cycle, and the bindings under the picture.</summary>
        [Fact]
        public void The_controls_inside_the_picture_are_the_sizes_the_canvas_draws()
        {
            Assert.Equal(132, PanelFacePlan.BarEndLeftWidth);
            Assert.Equal(146, PanelFacePlan.BarEndRightWidth);
            Assert.Equal(150, PanelFacePlan.BandSelectWidth);
            Assert.Equal(10, PanelFacePlan.BandGap);
            Assert.Equal(11, PanelFacePlan.CountCaptionSize);
            Assert.Equal(12, PanelFacePlan.CountChevronSize);
            Assert.Equal(22, PanelFacePlan.BindingGap);
            Assert.Equal(200, PanelFacePlan.GlanceSelectWidth);
            Assert.Equal(12, PanelFacePlan.GlanceBinderGap);
            Assert.True(PanelFacePlan.CountCaptionSize < Theme.SizeLabel,
                "a count is set smaller than a value, which is what tells the two apart in a cell");
            Assert.True(PanelFacePlan.CountChevronSize < Theme.IconSize,
                "and under a chevron smaller than every other icon on the panel");
        }

        /// <summary>The rows the face's own rectangles are too small to carry are the two that hold a
        /// control: the bar's least is the short control height rather than a number of its own.</summary>
        [Fact]
        public void A_row_is_never_smaller_than_what_it_has_to_hold()
        {
            Assert.Equal(26, PanelFacePlan.RevBarLeast);
            Assert.Equal(30, PanelFacePlan.BandLeast);
            Assert.Equal(138, PanelFacePlan.CellLeast);
            Assert.Equal(Theme.ControlHeightSm, PanelFacePlan.BarLeast);

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

        /// <summary>The cells and the seams between them fill the body exactly: a cell rounded on its own
        /// would leave a pixel of rule showing at the end of the row.</summary>
        [Fact]
        public void The_cells_and_their_seams_fill_the_body_exactly()
        {
            foreach (var face in Contract.FaceSizes)
            {
                var plan = PanelFacePlan.For(face);
                var span = plan.Stacked ? plan.Body : PanelFacePlan.PictureWidth;
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
