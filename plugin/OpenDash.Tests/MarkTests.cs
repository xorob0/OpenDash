// MarkTests.cs: the mark is drawn twice and has to stay one mark.
//
// media/logo.svg is the source; Ui.Mark redraws it in WPF, because WPF cannot render an SVG and Markdown cannot
// render a Canvas. The two now share one path string rather than a transcription of one, so most of this file is
// about the design rules the string has to keep -- the shape is easy to change and easy to break.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class MarkTests
    {
        static string Svg() => File.ReadAllText(RepoPaths.LogoSvg());

        static string SvgPathData()
        {
            var m = Regex.Match(Svg(), @"\sd=""(?<d>[^""]+)""");
            Assert.True(m.Success, "media/logo.svg draws the mark as one <path>, and it has no d");
            return m.Groups["d"].Value;
        }

        /// <summary>Every point the path actually moves to or draws to, which is not every number in it: an arc
        /// carries its radii and its flags in front of its endpoint.</summary>
        static IEnumerable<(double X, double Y)> Points(string d)
        {
            var tokens = Regex.Matches(d, @"[MLAZ]|-?\d*\.?\d+").Cast<Match>().Select(m => m.Value).ToList();
            double N(int i) => double.Parse(tokens[i], CultureInfo.InvariantCulture);
            for (var i = 0; i < tokens.Count; i++)
            {
                switch (tokens[i])
                {
                    case "M":
                    case "L":
                        yield return (N(i + 1), N(i + 2));
                        i += 2;
                        break;
                    case "A":
                        yield return (N(i + 6), N(i + 7)); // rx ry rotation large-arc sweep x y
                        i += 7;
                        break;
                }
            }
        }

        [Fact]
        public void The_panel_draws_the_path_the_svg_declares()
        {
            Assert.True(File.Exists(RepoPaths.LogoSvg()), RepoPaths.LogoSvg() + " is the mark and the panel is drawn from it");
            Assert.Equal(MarkShape.PathData, SvgPathData());
        }

        [Fact]
        public void The_needle_is_a_hole_in_the_housing_rather_than_a_second_shape()
        {
            // One subpath for the housing, one for the needle, and the even-odd rule between them. This is what
            // lets the mark carry a single brush and sit on any ground: the needle is whatever is behind it.
            // Ui.Mark asks for the same rule as "F0", which is the path mini-language's spelling of it.
            Assert.Contains(@"fill-rule=""evenodd""", Svg());
            Assert.Equal(2, MarkShape.PathData.Count(c => c == 'M'));
            Assert.Equal(2, MarkShape.PathData.Count(c => c == 'Z'));
        }

        [Fact]
        public void The_svg_is_square_and_the_mark_is_inside_it()
        {
            Assert.Contains("viewBox=\"0 0 " + MarkShape.Box + " " + MarkShape.Box + "\"", Svg());
            foreach (var (x, y) in Points(MarkShape.PathData))
            {
                Assert.InRange(x, 0, MarkShape.Box);
                Assert.InRange(y, 0, MarkShape.Box);
            }
        }

        [Fact]
        public void It_is_a_dash_rather_than_a_pod()
        {
            // Wider than it is tall, because that is the shape of the thing it stands for. Every face OpenDash
            // draws is a landscape rectangle, down to the 800 x 286 nano.
            Assert.True(MarkShape.HousingWidth > MarkShape.HousingHeight,
                "the housing is " + MarkShape.HousingWidth + " by " + MarkShape.HousingHeight);
        }

        [Fact]
        public void The_top_is_one_half_circle_and_the_bottom_is_square()
        {
            // A half circle, not two rounded corners: the radius is exactly half the width, so one arc spans the
            // whole of it. Anything less leaves a flat run across the top and the silhouette stops reading as a
            // dash. The floor keeps radius.none, which is what the brand says race dashes are.
            Assert.Equal(MarkShape.HousingWidth / 2, MarkShape.HousingRadius);
            Assert.True(MarkShape.HousingHeight > MarkShape.HousingRadius,
                "the half circle needs a straight side under it to sit on");

            var bottom = MarkShape.HousingY + MarkShape.HousingHeight;
            var corners = Points(MarkShape.PathData).Take(4).ToArray();
            Assert.Contains(corners, p => p.X == MarkShape.HousingX && p.Y == bottom);
            Assert.Contains(corners, p => p.X == MarkShape.HousingX + MarkShape.HousingWidth && p.Y == bottom);
        }

        [Fact]
        public void The_needle_pivots_at_the_centre_the_roof_is_drawn_from()
        {
            // The hub and the half circle share a centre, so the point aims at the arc wherever it is swept to.
            // It is also the only pivot that lets the needle be turned without redrawing anything else.
            Assert.Equal(MarkShape.HousingX + MarkShape.HousingWidth / 2, MarkShape.PivotX);
            Assert.Equal(MarkShape.HousingY + MarkShape.HousingRadius, MarkShape.PivotY);
        }

        [Fact]
        public void The_needle_is_a_needle_and_it_points_up_and_to_the_right()
        {
            // Up and to the right is a tachometer under load, which is the only state worth drawing. Between 30
            // and 75 degrees: flatter reads as idle, steeper as a exclamation mark rather than a needle.
            Assert.InRange(MarkShape.SweepDegrees, 30, 75);
            // Longer than the hub it leaves, or it is a blob with a spike rather than a needle.
            Assert.True(MarkShape.NeedleLength > 3 * MarkShape.HubRadius,
                "the needle is " + MarkShape.NeedleLength + " long off a hub of " + MarkShape.HubRadius);
        }

        [Fact]
        public void Nothing_in_it_merges_or_disappears_at_sixteen_pixels()
        {
            // The mark is shown at 16 px in a browser tab, where one unit is half a pixel. Two ways it can die
            // there, and the needle is what puts both in play: the hub can close up, and the point can touch the
            // housing and stop being a point. The needle's own taper is the one thing exempt -- it is meant to
            // reach nothing, and it is read from the body behind it.
            var px = 16.0 / MarkShape.Box;
            Assert.True(2 * MarkShape.HubRadius * px >= 2, "the hub is " + 2 * MarkShape.HubRadius + " units across and closes at 16 px");

            var clearance = MarkShape.HousingRadius - MarkShape.NeedleLength;
            Assert.True(clearance * px >= 1, "the point is " + clearance + " units off the arc and merges with it at 16 px");

            var belowHub = MarkShape.HousingY + MarkShape.HousingHeight - (MarkShape.PivotY + MarkShape.HubRadius);
            Assert.True(belowHub * px >= 1, "the hub is " + belowHub + " units off the floor and merges with it at 16 px");
        }

        [Fact]
        public void The_point_the_path_draws_is_the_one_the_constants_describe()
        {
            // The path is written out rather than generated, so this is what keeps the numbers in it honest: the
            // tip is the constants' own sweep and length, taken off the pivot.
            var a = MarkShape.SweepDegrees * Math.PI / 180;
            var expected = (X: MarkShape.PivotX + MarkShape.NeedleLength * Math.Cos(a),
                            Y: MarkShape.PivotY - MarkShape.NeedleLength * Math.Sin(a));
            var drawn = Points(MarkShape.PathData).Skip(4).ToArray();
            Assert.Contains(drawn, p => Math.Abs(p.X - expected.X) < 0.01 && Math.Abs(p.Y - expected.Y) < 0.01);
        }

        [Fact]
        public void It_carries_one_colour_and_it_is_the_panel_accent()
        {
            // A mark with a hex of its own would be a second palette. Theme.Accent mirrors purpose.ui.accent.
            var colours = Regex.Matches(Svg(), @"#[0-9A-Fa-f]{6}").Cast<Match>().Select(m => m.Value.ToUpperInvariant()).Distinct().ToArray();
            Assert.Equal(new[] { Theme.Accent.ToUpperInvariant() }, colours);
        }
    }
}
