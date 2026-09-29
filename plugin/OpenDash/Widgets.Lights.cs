// Widgets.Lights.cs: the kit's pictures -- a strip of LEDs, an 8x8 matrix, the Rig page's dotted ground and
// the schematic thumbnail a screen's card carries.
//
// Each draws what a pure model decided (PanelEmulation's frames, StripStyle and MatrixStyle's sizes), so the
// pictures on Home, Rig, LEDs, Matrix and the Add LEDs sheet are one drawing at five sizes. A null colour is
// an unlit LED and is painted in the style's unlit colour. The shell owns this file; pages build with it.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    internal static partial class Ui
    {
        /// <summary>
        /// A strip: its groups side by side, the LEDs of a group <see cref="StripStyle.Gap"/> apart and the
        /// groups <see cref="StripStyle.GroupGap"/> apart, on the style's ground and inside its frame.
        /// </summary>
        /// <param name="opacity">How bright the LEDs are drawn: PanelEmulation.Dim at night.</param>
        public static Border Strip(IList<string[]> groups, StripStyle style, double opacity = 1)
        {
            style = style ?? StripStyle.Card;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Opacity = opacity };
            if (groups != null)
            {
                for (var g = 0; g < groups.Count; g++)
                {
                    var group = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(g == 0 ? 0 : style.GroupGap, 0, 0, 0) };
                    var leds = groups[g] ?? new string[0];
                    for (var i = 0; i < leds.Length; i++)
                    {
                        group.Children.Add(new Border
                        {
                            Width = style.Led,
                            Height = style.Led,
                            CornerRadius = new CornerRadius(style.Radius),
                            Background = Brush(leds[i] ?? style.UnlitHex),
                            Margin = new Thickness(i == 0 ? 0 : style.Gap, 0, 0, 0),
                        });
                    }
                    row.Children.Add(group);
                }
            }
            return new Border
            {
                Background = style.GroundHex == null ? System.Windows.Media.Brushes.Transparent : Brush(style.GroundHex),
                BorderBrush = style.BorderHex == null ? null : Brush(style.BorderHex),
                BorderThickness = new Thickness(style.BorderHex == null ? 0 : PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(style.CornerRadius),
                Padding = new Thickness(style.PadX, style.PadY, style.PadX, style.PadY),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = row,
            };
        }

        /// <summary>
        /// An 8x8: sixty-four round cells, row by row, <see cref="MatrixStyle.Gap"/> apart on the style's
        /// ground. Drawn on a canvas at exact positions, so the grid is the same size at every scale.
        /// </summary>
        public static Border Matrix(IList<string> cells, MatrixStyle style, double opacity = 1)
        {
            style = style ?? MatrixStyle.Card;
            var side = 8 * style.Cell + 7 * style.Gap;
            var canvas = new Canvas { Width = side, Height = side, Opacity = opacity };
            for (var r = 0; r < 8; r++)
            {
                for (var c = 0; c < 8; c++)
                {
                    var index = r * 8 + c;
                    var colour = cells != null && index < cells.Count ? cells[index] : null;
                    var dot = new Ellipse { Width = style.Cell, Height = style.Cell, Fill = Brush(colour ?? style.UnlitHex) };
                    Canvas.SetLeft(dot, c * (style.Cell + style.Gap));
                    Canvas.SetTop(dot, r * (style.Cell + style.Gap));
                    canvas.Children.Add(dot);
                }
            }
            return new Border
            {
                Background = style.GroundHex == null ? System.Windows.Media.Brushes.Transparent : Brush(style.GroundHex),
                BorderBrush = style.BorderHex == null ? null : Brush(style.BorderHex),
                BorderThickness = new Thickness(style.BorderHex == null ? 0 : PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(style.CornerRadius),
                Padding = new Thickness(style.Pad),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = canvas,
            };
        }

        /// <summary>
        /// The Rig page's ground: a one pixel dot in the rule colour every 20 px, which is the artboard's
        /// radial-gradient tile.
        /// </summary>
        public static DrawingBrush DotGrid()
        {
            var step = PanelRigMap.GridStep;
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(System.Windows.Media.Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, step, step))));
            group.Children.Add(new GeometryDrawing(Brush(Theme.Rule), null, new EllipseGeometry(new Point(step / 2, step / 2), 1, 1)));
            var brush = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, step, step),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, step, step),
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None,
            };
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// A screen card's picture: the screen's outline at its own proportions inside a 40 px inset band,
        /// a ring for a round face.
        /// </summary>
        /// <param name="kind">A Contract kind, or "round" for a round face.</param>
        /// <param name="width">The screen's pixels across, for the proportion; zero draws a square.</param>
        /// <param name="height">The screen's pixels down.</param>
        public static Border Thumb(string kind, double width, double height)
        {
            const double band = 40;
            const double tall = 34;
            const double wide = 96;
            var round = string.Equals(kind, "round", StringComparison.OrdinalIgnoreCase);
            double w, h;
            if (round || width <= 0 || height <= 0)
            {
                w = tall;
                h = tall;
            }
            else if (width / height >= wide / tall)
            {
                w = wide;
                h = Math.Max(8, wide * height / width);
            }
            else
            {
                h = tall;
                w = Math.Max(8, tall * width / height);
            }
            FrameworkElement outline;
            if (round)
            {
                outline = new Ellipse { Width = w, Height = h, Stroke = Brush(Theme.TextLabel), StrokeThickness = PanelMetrics.BorderWeight };
            }
            else
            {
                outline = new Border
                {
                    Width = w,
                    Height = h,
                    BorderBrush = Brush(Theme.TextLabel),
                    BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                    CornerRadius = new CornerRadius(string.Equals(kind, Contract.KindCompanion, StringComparison.Ordinal) ? 3 : Theme.Radius),
                };
            }
            outline.HorizontalAlignment = HorizontalAlignment.Center;
            outline.VerticalAlignment = VerticalAlignment.Center;
            return new Border
            {
                Height = band,
                Background = Brush(Theme.SurfaceInset),
                CornerRadius = new CornerRadius(Theme.Radius),
                Child = outline,
            };
        }
    }
}
