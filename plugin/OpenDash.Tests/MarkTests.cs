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
        /// carries its radii and its flags in front of its endpoint. The mark draws no arcs any more, but the
        /// reader stays arc-aware so that a curve reintroduced by accident is measured rather than skipped.</summary>
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

        /// <summary>The points of the nth subpath, counting the housing as zero.</summary>
        static (double X, double Y)[] Subpath(int index) =>
            Points(MarkShape.PathData).Skip(4 * index).Take(4).ToArray();

        [Fact]
        public void The_panel_draws_the_path_the_svg_declares()
        {
            Assert.True(File.Exists(RepoPaths.LogoSvg()), RepoPaths.LogoSvg() + " is the mark and the panel is drawn from it");
            Assert.Equal(MarkShape.PathData, SvgPathData());
        }

        [Fact]
        public void The_separations_are_holes_in_the_housing_rather_than_lines_over_it()
        {
            // One subpath for the housing and one for each of the four separations, with the even-odd rule between
            // them. This is what lets the mark carry a single brush and sit on any ground: a separation is whatever
            // is behind it. Ui.Mark asks for the same rule as "F0", the path mini-language's spelling of it.
            Assert.Contains(@"fill-rule=""evenodd""", Svg());
            Assert.Equal(5, MarkShape.PathData.Count(c => c == 'M'));
            Assert.Equal(5, MarkShape.PathData.Count(c => c == 'Z'));
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
        public void Every_corner_is_square()
        {
            // radius.none throughout. The mark this replaced spent the one curve the brand allows itself on a half
            // circle over the top; a layout has no use for it, and an arc here would be the shape drifting back
            // towards the instrument it stopped being.
            Assert.DoesNotContain('A', MarkShape.PathData);

            var right = MarkShape.HousingX + MarkShape.HousingWidth;
            var bottom = MarkShape.HousingY + MarkShape.HousingHeight;
            var corners = Subpath(0);
            foreach (var expected in new[]
                     {
                         (X: MarkShape.HousingX, Y: MarkShape.HousingY),
                         (X: right, Y: MarkShape.HousingY),
                         (X: MarkShape.HousingX, Y: bottom),
                         (X: right, Y: bottom),
                     })
            {
                Assert.Contains(corners, p => p.X == expected.X && p.Y == expected.Y);
            }
        }

        [Fact]
        public void The_bands_run_the_full_width_and_only_the_middle_is_divided()
        {
            // This is the face's own structure rather than a grid: the header and the footer are uninterrupted,
            // and the three zones exist between them. A vertical slot that ran the whole height would draw three
            // columns of a table instead.
            var right = MarkShape.HousingX + MarkShape.HousingWidth;
            foreach (var band in new[] { Subpath(1), Subpath(2) })
            {
                Assert.All(band, p => Assert.True(p.X == MarkShape.HousingX || p.X == right,
                    "a band separation stops at " + p.X + " rather than running the full width"));
            }

            foreach (var column in new[] { Subpath(3), Subpath(4) })
            {
                Assert.All(column, p => Assert.True(p.Y == MarkShape.MiddleTop || p.Y == MarkShape.MiddleBottom,
                    "a column separation reaches " + p.Y + ", which is outside the middle band"));
            }
        }

        [Fact]
        public void The_gear_zone_is_the_widest_of_the_three()
        {
            // Hierarchy through size, which is the rule the face itself keeps: the gear is 260 px against 15 px
            // labels. Three equal columns would be a grid and would say nothing about what the dash is for.
            Assert.True(MarkShape.CentreWidth > MarkShape.SideWidth,
                "the centre zone is " + MarkShape.CentreWidth + " against " + MarkShape.SideWidth + " either side");

            // The header carries the rev bar and the session row, the footer one row of fuel figures.
            Assert.True(MarkShape.HeaderHeight > MarkShape.FooterHeight,
                "the header is " + MarkShape.HeaderHeight + " and the footer " + MarkShape.FooterHeight);
        }

        [Fact]
        public void Nothing_in_it_merges_or_disappears_at_twenty_four_pixels()
        {
            // The mark this replaced was held to 16 px, because a browser tab draws it there. A layout cannot meet
            // that and it was adopted knowing so: at half a pixel per unit the five zones close up and it reads as
            // a striped rectangle. 24 px is what it is held to instead, which is the size SimHub's left menu draws
            // it at, and at three quarters of a pixel per unit nothing here may fall under one and a half.
            var px = 24.0 / MarkShape.Box;
            foreach (var (name, units) in new (string, double)[]
                     {
                         ("the gap between zones", MarkShape.Gap),
                         ("the header", MarkShape.HeaderHeight),
                         ("the footer", MarkShape.FooterHeight),
                         ("a side zone", MarkShape.SideWidth),
                         ("the gear zone", MarkShape.CentreWidth),
                         ("the middle band", MarkShape.MiddleBottom - MarkShape.MiddleTop),
                     })
            {
                Assert.True(units * px >= 1.5, name + " is " + units + " units and draws at " + units * px + " px");
            }
        }

        [Fact]
        public void The_slots_the_path_draws_are_the_ones_the_constants_describe()
        {
            // The path is written out rather than generated, so this is what keeps the numbers in it honest: the
            // four separations are the constants' own widths, taken off the housing's edges.
            Assert.Equal(new[] { MarkShape.HousingY + MarkShape.HeaderHeight, MarkShape.MiddleTop },
                Subpath(1).Select(p => p.Y).Distinct().OrderBy(y => y).ToArray());
            Assert.Equal(new[] { MarkShape.MiddleBottom, MarkShape.HousingY + MarkShape.HousingHeight - MarkShape.FooterHeight },
                Subpath(2).Select(p => p.Y).Distinct().OrderBy(y => y).ToArray());

            var left = MarkShape.HousingX + MarkShape.SideWidth;
            Assert.Equal(new[] { left, left + MarkShape.Gap },
                Subpath(3).Select(p => p.X).Distinct().OrderBy(x => x).ToArray());

            var rightSlot = left + MarkShape.Gap + MarkShape.CentreWidth;
            Assert.Equal(new[] { rightSlot, rightSlot + MarkShape.Gap },
                Subpath(4).Select(p => p.X).Distinct().OrderBy(x => x).ToArray());
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
