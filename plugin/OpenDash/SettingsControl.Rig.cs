// SettingsControl.Rig.cs: the Rig page -- every screen, strip and matrix as a tile on a dotted ground, and a
// row of chips that paints a flag, a car alongside, the pit lane, a warning or the revs on every tile at once.
//
// "Zone C" and "matrix 2" mean nothing until you see where they are, and the only way to check a flag used to
// be to own the hardware and wait for one (#503). The #503 foundation draws the tiles where PanelRigMap lays
// them out and paints them from PanelEmulation, which reads the flag box's own glyphs; the Rig page agent owns
// this file and adds dragging the tiles into the shape of the rig and keeping where they were put. Nothing here
// lights the real hardware: that is #506, greyed in the header.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>
        /// The scenario the tiles are painted with: the Rig page's selection, which is a PanelEmulation
        /// scenario id, so another page opens Rig on one with Open(PanelPage.Rig, id) -- Settings' "Try".
        /// </summary>
        private string rigScenario
        {
            get
            {
                var id = Selected(PanelPage.Rig);
                return id != null && PanelEmulation.Find(id) != null ? id : PanelEmulation.Default;
            }
        }

        /// <summary>Whether a hand can move the tiles. False until the Rig page agent lands drag, which
        /// turns it on together with the canvas's hint.</summary>
        private static readonly bool RigTilesDraggable = false;

        private FrameworkElement BuildRigPage(PanelRoute to)
        {
            DrawsLighting();
            var night = Ui.Switch(Settings.LightsNightMode, on =>
            {
                Settings.LightsNightMode = on;
                Save();
                ShowLightingChange();
            });
            var real = PanelSoon.Find(PanelRigMap.RealHardwareTitle);
            var hardware = Ui.Soon(Ui.HStack(10, Ui.Text(real.Title, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary), Ui.SoonTag(), Ui.Switch(false, null)), real);
            var actions = Ui.HStack(18, Ui.HStack(10, Ui.Text("Night mode", Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary), night), hardware);

            var title = Ui.HStack(12, Ui.PageTitle(PanelRigMap.Title), Ui.NewTag());
            var head = Ui.Row(title, actions);
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            var canvas = Ui.Anchor(BuildRigCanvas(), PanelRigMap.AnchorCanvas);
            canvas.Margin = new Thickness(0, 18, 0, 0);
            stack.Children.Add(canvas);
            var chips = Ui.Anchor(BuildScenarioChips(), PanelRigMap.AnchorScenarios);
            chips.Margin = new Thickness(0, 18, 0, 0);
            stack.Children.Add(chips);
            return stack;
        }

        /// <summary>The dotted ground and every tile on it, where PanelRigMap puts a tile nobody has moved.</summary>
        private FrameworkElement BuildRigCanvas()
        {
            var width = Math.Max(320, ContentWidth);
            var canvas = new Canvas { Height = PanelRigMap.CanvasHeight, ClipToBounds = true };
            var tiles = PanelRigMap.AutoLayout(RigTiles(), width);
            foreach (var tile in tiles)
            {
                var drawn = BuildRigTile(tile);
                Canvas.SetLeft(drawn, tile.X);
                Canvas.SetTop(drawn, tile.Y);
                canvas.Children.Add(drawn);
            }
            // The hint tells the driver to drag, so it is drawn only once the tiles can be dragged.
            if (RigTilesDraggable)
            {
                var hint = Ui.Text(PanelRigMap.CanvasHint, Theme.SizeLabel, FontWeights.Normal, Theme.TextLabel);
                Canvas.SetLeft(hint, 14);
                Canvas.SetBottom(hint, 12);
                canvas.Children.Add(hint);
            }
            if (tiles.Count == 0)
            {
                var screensPress = Ui.Button("Open Screens", PanelButtonKind.Outline, PanelButtonSize.Small);
                screensPress.Click += (sender, args) => Go(PanelPage.Screens);
                var empty = Ui.HStack(12, Ui.Prose(PanelRigMap.Empty, Theme.SizeBody), screensPress);
                Canvas.SetLeft(empty, 24);
                Canvas.SetTop(empty, 24);
                canvas.Children.Add(empty);
            }
            return new Border
            {
                Height = PanelRigMap.CanvasHeight,
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Child = new Border { Background = Ui.DotGrid(), Child = canvas },
            };
        }

        /// <summary>Every device on the rig as a tile, at the size its picture is drawn.</summary>
        private IList<RigTile> RigTiles()
        {
            var tiles = new List<RigTile>();
            foreach (var screen in Settings.RigScreens())
            {
                if (screen.IsCompanion) tiles.Add(new RigTile(RigTileKind.Companion, screen.Namespace, screen.Name, 0, 0, PanelRigMap.CompanionWidth, PanelRigMap.CompanionHeight));
                else if (screen.IsPitWall) tiles.Add(new RigTile(RigTileKind.PitWall, screen.Namespace, screen.Name, 0, 0, PanelRigMap.PitWallWidth, PanelRigMap.PitWallWidth * 9 / 16));
                else if (screen.IsSlots) tiles.Add(new RigTile(RigTileKind.Round, screen.Namespace, screen.Name, 0, 0, PanelRigMap.RoundSize, PanelRigMap.RoundSize));
                else
                {
                    var aspect = screen.Width > 0 && screen.Height > 0 ? (double)screen.Height / screen.Width : 480.0 / 1280.0;
                    var w = aspect > 1 ? PanelRigMap.FaceWidth / 2 : PanelRigMap.FaceWidth;
                    tiles.Add(new RigTile(RigTileKind.Face, screen.Namespace, screen.Name, 0, 0, w, Math.Max(60, w * aspect)));
                }
            }
            foreach (var bar in Settings.LedBarList())
            {
                var shape = LightShape.Parse(bar.Shape);
                var leds = shape == null ? 9 : shape.Left + shape.Centre + shape.Right;
                var groups = shape == null || shape.Bare ? 1 : 3;
                var style = StripStyle.Rig;
                var w = leds * style.Led + Math.Max(0, leds - groups) * style.Gap + (groups - 1) * style.GroupGap + 2 * style.PadX + 2;
                tiles.Add(new RigTile(RigTileKind.Strip, bar.Namespace, bar.Name, 0, 0, w, style.Led + 2 * style.PadY + 2));
            }
            foreach (var matrix in Settings.MatrixPanels())
            {
                var side = MatrixStyle.Rig.Side + 2;
                tiles.Add(new RigTile(RigTileKind.Matrix, matrix.ToString(System.Globalization.CultureInfo.InvariantCulture), Settings.MatrixName(matrix) ?? "Matrix " + matrix, 0, 0, side, side));
            }
            return tiles;
        }

        /// <summary>One tile: its name over its picture, painted with the scenario.</summary>
        private FrameworkElement BuildRigTile(RigTile tile)
        {
            var name = Ui.Text(tile.Name, Theme.SizeLabel, FontWeights.Medium, Theme.TextSecondary);
            name.Height = PanelRigMap.NameHeight;
            name.Margin = new Thickness(0, 0, 0, PanelRigMap.NameGap);
            var lights = PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);
            FrameworkElement picture;
            switch (tile.Kind)
            {
                case RigTileKind.Strip:
                    var bar = Settings.LedBarByNamespace(tile.Id);
                    var shape = bar == null ? null : LightShape.Parse(bar.Shape);
                    var options = new StripOptions { SpotterWhole = bar != null && bar.SpotterWhole };
                    if (bar != null && bar.EffectsOff != null) foreach (var off in bar.EffectsOff) options.EffectsOff.Add(off);
                    picture = Ui.Strip(PanelEmulation.StripFrame(shape == null ? 3 : shape.Left, shape == null ? 9 : shape.Centre, rigScenario, options), StripStyle.Rig, lights);
                    break;
                case RigTileKind.Matrix:
                    int slot;
                    int.TryParse(tile.Id, out slot);
                    var matrix = new MatrixOptions
                    {
                        Rest = Settings.MatrixRest(slot),
                        Side = Settings.MatrixSide(slot),
                        Bands = Settings.MatrixGearBands(slot),
                        Flags = Settings.MatrixFlags(slot),
                        Pit = Settings.MatrixPit(slot),
                        Spotter = Settings.MatrixSpotter(slot),
                        Warnings = Settings.MatrixWarnings(slot),
                    };
                    picture = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, rigScenario, matrix), MatrixStyle.Rig, lights);
                    break;
                case RigTileKind.Round:
                    picture = new Ellipse
                    {
                        Width = tile.Width,
                        Height = tile.Height,
                        Fill = Ui.Brush(Theme.SurfaceInset),
                        Stroke = Ui.Brush(PanelEmulation.RingColour(rigScenario)),
                        StrokeThickness = 6,
                    };
                    break;
                case RigTileKind.Companion:
                    picture = ScreenTile(tile, PanelEmulation.Band(rigScenario), true);
                    break;
                default:
                    picture = ScreenTile(tile, PanelEmulation.Band(rigScenario), false);
                    break;
            }
            return Ui.VStack(0, name, picture);
        }

        /// <summary>
        /// A screen's tile: the revs across its top, three zones, and its band -- or, for a companion, the
        /// flag over the whole of it -- in the dash's own words for the scenario.
        /// </summary>
        private FrameworkElement ScreenTile(RigTile tile, FaceBand band, bool companion)
        {
            var body = new DockPanel { LastChildFill = true };
            if (companion)
            {
                var fill = band.Alert && band.FillHex != null && !band.Outlined ? band.FillHex : Theme.SurfaceInset;
                var words = band.Alert ? band.Text ?? string.Empty : "Relative";
                return new Border
                {
                    Width = tile.Width,
                    Height = tile.Height,
                    CornerRadius = new CornerRadius(6),
                    BorderBrush = Ui.Brush(band.Outlined ? band.FillHex : Theme.Border),
                    BorderThickness = new Thickness(band.Outlined ? 3 : PanelMetrics.BorderWeight),
                    Background = Ui.Brush(fill),
                    Child = new TextBlock
                    {
                        Text = words,
                        FontFamily = PanelFonts.Label,
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Ui.Brush(band.Alert && !band.Outlined ? band.TextHex : Theme.TextSecondary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                    },
                };
            }

            var revs = PanelEmulation.Revs(20, rigScenario);
            var revRow = new UniformGrid { Rows = 1, Columns = revs.Length, Height = 9, Margin = new Thickness(0, 0, 0, 3) };
            foreach (var led in revs) revRow.Children.Add(new Border { Margin = new Thickness(1, 1, 1, 1), CornerRadius = new CornerRadius(1), Background = Ui.Brush(led ?? Theme.SurfaceRaised) });
            DockPanel.SetDock(revRow, Dock.Top);
            body.Children.Add(revRow);

            var bandHeight = Math.Max(14, Math.Round(tile.Height * 0.18));
            var bandBox = new Border
            {
                Height = bandHeight,
                Margin = new Thickness(0, 3, 0, 0),
                Background = Ui.Brush(band.Alert && !band.Outlined && band.FillHex != null ? band.FillHex : band.Chequer ? Theme.FlagChequer : Theme.SurfaceZone),
                BorderBrush = band.Outlined ? Ui.Brush(band.FillHex) : null,
                BorderThickness = new Thickness(band.Outlined ? 2 : 0),
                Child = new TextBlock
                {
                    Text = band.Alert ? band.Text ?? string.Empty : "Band D",
                    FontFamily = PanelFonts.Label,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Ui.Brush(band.Alert ? band.TextHex : Theme.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            DockPanel.SetDock(bandBox, Dock.Bottom);
            body.Children.Add(bandBox);

            var zones = new Grid();
            zones.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            zones.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });
            zones.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            for (var i = 0; i < 3; i++)
            {
                var zone = new Border { Background = Ui.Brush(Theme.SurfaceZone), Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0) };
                Grid.SetColumn(zone, i);
                zones.Children.Add(zone);
            }
            body.Children.Add(zones);

            return new Border
            {
                Width = tile.Width,
                Height = tile.Height,
                Padding = new Thickness(4),
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(3),
                Child = body,
            };
        }

        /// <summary>The chips, in PanelEmulation's five groups; picking one paints every tile with it.</summary>
        private FrameworkElement BuildScenarioChips()
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var group in PanelEmulation.Groups)
            {
                var chips = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                foreach (var scenario in group.Scenarios)
                {
                    var id = scenario.Id;
                    var chip = Ui.SwatchChip(scenario.Label, scenario.SwatchHex, id == rigScenario, () =>
                    {
                        Select(PanelPage.Rig, id);
                        RebuildPage();
                    });
                    chip.Margin = new Thickness(0, 0, 6, 6);
                    chips.Children.Add(chip);
                }
                var column = Ui.VStack(0, Ui.Eyebrow(group.Title), chips);
                column.Margin = new Thickness(0, 0, 32, 12);
                wrap.Children.Add(column);
            }
            return wrap;
        }
    }
}
