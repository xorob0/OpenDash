// MarkTests.cs: the mark is drawn twice and has to stay one mark.
//
// media/logo.svg is the source; Ui.Mark redraws the same rectangles in WPF, because WPF cannot render an SVG and
// Markdown cannot render a Canvas. Two copies of a shape drift, so this reads the file and checks the numbers.
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class MarkTests
    {
        [Fact]
        public void The_panel_draws_the_rectangles_the_svg_declares()
        {
            var path = RepoPaths.LogoSvg();
            Assert.True(File.Exists(path), path + " is the mark and the panel is drawn from it");
            var svg = File.ReadAllText(path);

            var rects = Regex.Matches(svg, @"<rect\s+x=""(?<x>[-\d.]+)""\s*y=""(?<y>[-\d.]+)""\s*width=""(?<w>[-\d.]+)""\s*height=""(?<h>[-\d.]+)""")
                .Cast<Match>()
                .Select(m => new[]
                {
                    double.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture),
                    double.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture),
                    double.Parse(m.Groups["w"].Value, CultureInfo.InvariantCulture),
                    double.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture),
                })
                .ToArray();

            Assert.Equal(MarkShape.Bars.Length, rects.Length);
            for (var i = 0; i < rects.Length; i++) Assert.Equal(MarkShape.Bars[i], rects[i]);
        }

        [Fact]
        public void The_svg_is_square_and_the_bars_are_inside_it()
        {
            var svg = File.ReadAllText(RepoPaths.LogoSvg());
            Assert.Contains("viewBox=\"0 0 " + MarkShape.Box + " " + MarkShape.Box + "\"", svg);
            foreach (var bar in MarkShape.Bars)
            {
                Assert.InRange(bar[0], 0, MarkShape.Box);
                Assert.InRange(bar[1], 0, MarkShape.Box);
                Assert.InRange(bar[0] + bar[2], 0, MarkShape.Box);
                Assert.InRange(bar[1] + bar[3], 0, MarkShape.Box);
            }
        }

        [Fact]
        public void Nothing_in_it_disappears_at_sixteen_pixels()
        {
            // The mark is shown at 16 px in a browser tab. A bar thinner than half a pixel there is a bar that is
            // not drawn, so the narrowest is held to four units of thirty-two, which is two pixels at that size.
            var narrowest = MarkShape.Bars.Min(bar => System.Math.Min(bar[2], bar[3]));
            Assert.True(narrowest * 16 / MarkShape.Box >= 1, "the narrowest bar is " + narrowest + " units and vanishes at 16 px");
        }

        [Fact]
        public void Nothing_in_it_merges_at_sixteen_pixels()
        {
            // The other way the mark dies small. A gap that closes turns four segments into one blob, which the
            // bar-width rule alone does not catch: the first mark held its bars to five units and its gaps to two.
            var gaps = Gaps();
            var narrowest = gaps.Min();
            Assert.True(narrowest * 16 / MarkShape.Box >= 1, "the narrowest gap is " + narrowest + " units and closes at 16 px");
        }

        [Fact]
        public void The_shift_point_stands_off_a_wider_gap_than_the_segments_it_follows()
        {
            // The mark's one asymmetry, and the whole of what makes it a rev bar rather than a row of segments.
            // The first mark claimed a "separated fifth" and spaced it exactly like the other four, so it read as
            // a hairline on the end of the run rather than as the shift point.
            var gaps = Gaps();
            var separation = gaps.Last();
            Assert.True(gaps.SkipLast(1).All(g => separation > g),
                "the shift point is spaced " + separation + " and the segments " + string.Join(", ", gaps.SkipLast(1)));
        }

        [Fact]
        public void It_sweeps_rather_than_charts()
        {
            // Three progressions, and none of them is a baseline. Bars standing on a shared baseline read as a
            // measurement whatever else is done to them, so every segment starts higher, ends higher and is taller
            // than the one before it.
            foreach (var (a, b) in MarkShape.Bars.Zip(MarkShape.Bars.Skip(1)))
            {
                Assert.True(b[1] < a[1], "segment tops have to climb");
                Assert.True(b[1] + b[3] < a[1] + a[3], "segment bottoms have to climb, or the mark is a bar chart");
                Assert.True(b[3] > a[3], "segment heights have to grow");
            }
        }

        [Fact]
        public void It_is_hard_edged_in_both_copies()
        {
            // radius.none: race dashes are not rounded. This is also where the two copies had already drifted --
            // the SVG asked for rx 2 and the panel drew RadiusX = width / 2, a capsule -- because the test above
            // compares positions and sizes and a corner is neither.
            var svg = File.ReadAllText(RepoPaths.LogoSvg());
            Assert.DoesNotContain("rx=", svg);
            Assert.DoesNotContain("ry=", svg);
            Assert.Equal(4, MarkShape.Bars[0].Length); // x, y, width, height: there is no radius to draw.
        }

        [Fact]
        public void It_carries_one_colour_and_it_is_the_panel_accent()
        {
            // A mark with a hex of its own would be a second palette. Theme.Accent mirrors purpose.ui.accent.
            var svg = File.ReadAllText(RepoPaths.LogoSvg());
            var colours = Regex.Matches(svg, @"#[0-9A-Fa-f]{6}").Cast<Match>().Select(m => m.Value.ToUpperInvariant()).Distinct().ToArray();
            Assert.Equal(new[] { Theme.Accent.ToUpperInvariant() }, colours);
        }

        /// <summary>The horizontal gap between each segment and the next, in the SVG's units.</summary>
        static double[] Gaps()
        {
            return MarkShape.Bars.Zip(MarkShape.Bars.Skip(1), (a, b) => b[0] - (a[0] + a[2])).ToArray();
        }
    }
}
