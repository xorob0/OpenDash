// SettingsControl.Rig.cs: the Rig page -- every screen, strip and matrix as a tile on a dotted ground the
// driver drags into the shape of the rig, and a row of chips that paints a flag, a car alongside, the pit
// lane, a warning or the revs on every tile at once.
//
// "Zone C" and "matrix 2" mean nothing until you see where they are, and the only way to check a flag used to
// be to own the hardware and wait for one (#503). Rig.dc.html is the artboard. Every size, word and rule is
// PanelRigMap's, where PanelRigMapTests holds it; this file only draws. A tile is where the driver dropped it
// (ScreenInstance's, LedBar's or the matrix slot's LayoutX and LayoutY, saved on the drop) or where
// PanelRigMap.DefaultLayout puts it. A chip repaints the tiles' pictures in place and rebuilds nothing.
// Nothing here lights the real hardware: that is #506, greyed in the header.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

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

        /// <summary>The z-order the last tile picked up was raised to, so the tile in the hand is on top.</summary>
        private int rigTopZ;

        /// <summary>One tile as drawn: its place on the canvas, and how to paint its picture again.</summary>
        private sealed class RigTileView
        {
            public RigTileView(RigTile tile, FrameworkElement element, Action<string> paint)
            {
                Tile = tile;
                Element = element;
                Paint = paint;
            }

            public RigTile Tile { get; private set; }
            public FrameworkElement Element { get; private set; }

            /// <summary>Repaints the tile's picture under a scenario, in place.</summary>
            public Action<string> Paint { get; private set; }
        }

        private FrameworkElement BuildRigPage(PanelRoute to)
        {
            // The header draws the night switch, so a wheel's night-mode press rebuilds the page and the switch
            // and the lights' dim follow it.
            DrawsLighting();
            var views = new List<RigTileView>();
            var canvas = Ui.Anchor(BuildRigCanvas(views), PanelRigMap.AnchorCanvas);
            var chips = Ui.Anchor(BuildRigScenarios(views), PanelRigMap.AnchorScenarios);

            var gap = PanelShell.SectionGapFor(PanelPage.Rig);
            canvas.Margin = new Thickness(0, gap, 0, 0);
            chips.Margin = new Thickness(0, gap, 0, 0);
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(BuildRigHeader());
            stack.Children.Add(canvas);
            stack.Children.Add(chips);
            return stack;
        }

        /// <summary>The title and its New tag, and on its right the night switch, the greyed Real hardware
        /// switch and Reset layout; under the title when there is not the room for both on one line.</summary>
        private FrameworkElement BuildRigHeader()
        {
            var title = Ui.HStack(PanelRigMap.TitleTagGap, Ui.PageTitle(PanelRigMap.Title), Ui.NewTag());

            var night = Ui.Switch(Settings.LightsNightMode, on =>
            {
                Settings.LightsNightMode = on;
                Save();
                ShowLightingChange();
            });
            AutomationProperties.SetName(night, PanelSettings.NightModeTitle);
            var nightGroup = Ui.HStack(PanelRigMap.HeaderLabelGap, RigHeaderLabel(PanelSettings.NightModeTitle), night);

            var real = PanelSoon.RealHardware;
            var hardware = Ui.Soon(Ui.HStack(PanelRigMap.HeaderLabelGap, RigHeaderLabel(real.Title), Ui.SoonTag(real), Ui.Switch(false, null)), real);

            var reset = Ui.Button(PanelRigMap.ResetLayout, PanelButtonKind.Outline, PanelButtonSize.Small);
            reset.Height = PanelRigMap.ResetButtonHeight;
            reset.Padding = new Thickness(PanelRigMap.ResetButtonPaddingX, 0, PanelRigMap.ResetButtonPaddingX, 0);
            reset.Click += (sender, args) => RigResetLayout();

            var controls = new FrameworkElement[] { nightGroup, hardware, reset };
            if (TwoColumns)
            {
                var right = Ui.HStack(PanelRigMap.HeaderGap, controls);
                right.VerticalAlignment = VerticalAlignment.Center;
                return Ui.Row(title, right);
            }

            // Stacked: the controls wrap under the title, so a narrow column never pushes one off the page.
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, PanelRigMap.HeaderStackGap, 0, 0) };
            foreach (var control in controls)
            {
                control.Margin = new Thickness(0, 0, PanelRigMap.HeaderGap, PanelRigMap.HeaderLabelGap);
                control.VerticalAlignment = VerticalAlignment.Center;
                wrap.Children.Add(control);
            }
            return Ui.VStack(0, title, wrap);
        }

        private static TextBlock RigHeaderLabel(string text)
        {
            var label = Ui.Text(text, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        /// <summary>Forgets where every tile was put, and draws the canvas in its default layout again.</summary>
        private void RigResetLayout()
        {
            PanelRigMap.ClearLayout(Settings);
            Save();
            Redraw();
        }

        /// <summary>The dotted ground and every tile on it, where the driver put it or where PanelRigMap puts a
        /// tile nobody has moved. Fills <paramref name="views"/> with what the chips repaint.</summary>
        private FrameworkElement BuildRigCanvas(IList<RigTileView> views)
        {
            var width = Math.Max(0, ContentWidth - 2 * PanelMetrics.BorderWeight);
            var canvas = new Canvas { Height = PanelRigMap.CanvasHeight, ClipToBounds = true, Background = Ui.DotGrid() };
            var tiles = PanelRigMap.Arrange(Settings, width, PanelRigMap.CanvasHeight);

            if (tiles.Count == 0)
            {
                var screensPress = Ui.Button(PanelAttention.Open(PanelScreens.Title), PanelButtonKind.Outline, PanelButtonSize.Small);
                screensPress.Click += (sender, args) => Go(PanelPage.Screens);
                var empty = Ui.HStack(12, Ui.Prose(PanelRigMap.Empty, Theme.SizeBody), screensPress);
                Canvas.SetLeft(empty, PanelRigMap.LayoutMargin);
                Canvas.SetTop(empty, PanelRigMap.LayoutMargin);
                canvas.Children.Add(empty);
            }
            else
            {
                // Under the tiles, so a tile dragged into the corner covers it rather than the other way round.
                var hint = Ui.Text(PanelRigMap.CanvasHint, Theme.SizeLabel, FontWeights.Normal, Theme.TextLabel);
                Canvas.SetLeft(hint, PanelRigMap.HintLeft);
                Canvas.SetBottom(hint, PanelRigMap.HintBottom);
                canvas.Children.Add(hint);
            }

            var scenario = rigScenario;
            foreach (var tile in tiles)
            {
                var view = BuildRigTile(tile, canvas, width, scenario);
                views.Add(view);
                canvas.Children.Add(view.Element);
            }

            return new Border
            {
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Child = canvas,
            };
        }

        /// <summary>
        /// One tile: its name (and the warning dot, when the device is on Home's list) over its picture, with a
        /// transparent Thumb over the whole of it that takes the drag and the arrow keys.
        /// </summary>
        private RigTileView BuildRigTile(RigTile tile, Canvas canvas, double canvasWidth, string scenario)
        {
            var warns = PanelRigMap.Warns(tile, issues);
            var name = Ui.Text(tile.Name, PanelRigMap.NameSize, FontWeights.Medium, Theme.TextSecondary);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.MaxWidth = Math.Max(0, tile.Width - (warns ? PanelRigMap.WarnDot + PanelRigMap.WarnGap : 0));
            var nameLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Height = PanelRigMap.NameHeight,
                Margin = new Thickness(0, 0, 0, PanelRigMap.NameGap),
            };
            nameLine.Children.Add(name);
            if (warns)
            {
                nameLine.Children.Add(new Ellipse
                {
                    Width = PanelRigMap.WarnDot,
                    Height = PanelRigMap.WarnDot,
                    Fill = Ui.Brush(Theme.Caution),
                    Margin = new Thickness(PanelRigMap.WarnGap, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            var host = new Border { Width = tile.Width, Height = tile.Height, HorizontalAlignment = HorizontalAlignment.Left };
            Action<string> paint = id => host.Child = BuildRigPicture(tile, id);
            paint(scenario);

            var body = new StackPanel { Orientation = Orientation.Vertical };
            body.Children.Add(nameLine);
            body.Children.Add(host);

            var thumb = new Thumb
            {
                Template = RigThumbTemplate,
                Cursor = Cursors.SizeAll,
                Focusable = true,
                IsTabStop = true,
                FocusVisualStyle = Ui.FocusRing(),
            };
            AutomationProperties.SetName(thumb, tile.Name);

            var root = new Grid { Width = PanelRigMap.FootprintWidth(tile), Height = PanelRigMap.FootprintHeight(tile) };
            root.Children.Add(body);
            root.Children.Add(thumb);
            Canvas.SetLeft(root, tile.X);
            Canvas.SetTop(root, tile.Y);

            Func<double> extentX = () => canvas.ActualWidth > 0 ? canvas.ActualWidth : canvasWidth;
            Func<double> extentY = () => canvas.ActualHeight > 0 ? canvas.ActualHeight : PanelRigMap.CanvasHeight;
            var moved = false;
            thumb.DragStarted += (sender, args) =>
            {
                moved = false;
                Panel.SetZIndex(root, ++rigTopZ);
            };
            thumb.DragDelta += (sender, args) =>
            {
                if (args.HorizontalChange == 0 && args.VerticalChange == 0) return;
                moved = true;
                Canvas.SetLeft(root, PanelRigMap.Clamp(Canvas.GetLeft(root) + args.HorizontalChange, root.Width, extentX()));
                Canvas.SetTop(root, PanelRigMap.Clamp(Canvas.GetTop(root) + args.VerticalChange, root.Height, extentY()));
            };
            // Saved on the drop, never in End(): SimHub is force-killed on the VM.
            thumb.DragCompleted += (sender, args) =>
            {
                if (!moved) return;
                RigDrop(tile, root, Canvas.GetLeft(root), Canvas.GetTop(root), extentX(), extentY());
            };
            thumb.KeyDown += (sender, args) =>
            {
                double dx = 0, dy = 0;
                switch (args.Key)
                {
                    case Key.Left: dx = -PanelRigMap.GridStep; break;
                    case Key.Right: dx = PanelRigMap.GridStep; break;
                    case Key.Up: dy = -PanelRigMap.GridStep; break;
                    case Key.Down: dy = PanelRigMap.GridStep; break;
                    default: return;
                }
                args.Handled = true;
                Panel.SetZIndex(root, ++rigTopZ);
                RigDrop(tile, root, Canvas.GetLeft(root) + dx, Canvas.GetTop(root) + dy, extentX(), extentY());
            };

            return new RigTileView(tile, root, paint);
        }

        /// <summary>Puts a dropped tile on the nearest dot inside the canvas and keeps it there.</summary>
        private void RigDrop(RigTile tile, FrameworkElement root, double x, double y, double extentX, double extentY)
        {
            var left = PanelRigMap.Clamp(PanelRigMap.Snap(x), root.Width, extentX);
            var top = PanelRigMap.Clamp(PanelRigMap.Snap(y), root.Height, extentY);
            Canvas.SetLeft(root, left);
            Canvas.SetTop(root, top);
            PanelRigMap.SavePosition(Settings, tile, left, top);
            Save();
        }

        /// <summary>The Thumb over a tile: nothing to see, so the tile shows through and the drag is the tile's.
        /// Made on first use, on the interface thread, rather than whenever the class is first touched.</summary>
        private static ControlTemplate RigThumbTemplate
        {
            get
            {
                if (rigThumbTemplate == null)
                {
                    var face = new FrameworkElementFactory(typeof(Border));
                    face.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                    var template = new ControlTemplate(typeof(Thumb)) { VisualTree = face };
                    template.Seal();
                    rigThumbTemplate = template;
                }
                return rigThumbTemplate;
            }
        }

        private static ControlTemplate rigThumbTemplate;

        /// <summary>How bright the lights are drawn: the night brightness at night. The screens do not dim (#128).</summary>
        private double RigLights()
        {
            return PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);
        }

        /// <summary>A tile's picture under a scenario, from that device's own settings.</summary>
        private FrameworkElement BuildRigPicture(RigTile tile, string scenario)
        {
            switch (tile.Kind)
            {
                case RigTileKind.Strip:
                    var bar = Settings.LedBarByNamespace(tile.Key);
                    var frame = PanelEmulation.StripFrame(PanelRigMap.StripEnds(bar), PanelRigMap.StripCentre(bar), scenario, PanelRigMap.StripOptionsFor(bar));
                    return Ui.Strip(frame, StripStyle.Rig, RigLights());
                case RigTileKind.Matrix:
                    var options = PanelRigMap.MatrixOptionsFor(Settings, PanelRigMap.MatrixSlot(tile));
                    return Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, scenario, options), MatrixStyle.Rig, RigLights());
                case RigTileKind.Round:
                    return RigRound(tile, scenario);
                case RigTileKind.Companion:
                    return RigCompanion(tile, Settings.ScreenByNamespace(tile.Key), scenario);
                case RigTileKind.PitWall:
                    return RigPitWall(tile, Settings.ScreenByNamespace(tile.Key), scenario);
                default:
                    return RigFace(tile, Settings.ScreenByNamespace(tile.Key), scenario);
            }
        }

        /// <summary>A screen's frame: inset ground, the border, 4 in.</summary>
        private static Border RigScreenFrame(RigTile tile, UIElement child)
        {
            return new Border
            {
                Width = tile.Width,
                Height = tile.Height,
                Padding = new Thickness(PanelRigMap.ScreenPadding),
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(PanelRigMap.ScreenRadius),
                Child = child,
            };
        }

        /// <summary>
        /// A face: its rev strip across the top, its three zones on the pages they are on, and its band, in
        /// the dash's own words for the scenario.
        /// </summary>
        private FrameworkElement RigFace(RigTile tile, ScreenInstance screen, string scenario)
        {
            var dock = new DockPanel { LastChildFill = true };

            var revs = PanelRigMap.FaceRevs(scenario, screen == null ? Settings.RevBarMode() : Settings.ScreenRevBar(screen.Namespace));
            var segments = new UniformGrid { Rows = 1, Columns = revs.Length };
            foreach (var led in revs)
            {
                segments.Children.Add(new Border
                {
                    Margin = new Thickness(PanelRigMap.RevGap / 2, 0, PanelRigMap.RevGap / 2, 0),
                    CornerRadius = new CornerRadius(1),
                    Background = Ui.Brush(led ?? Theme.SurfaceRaised),
                });
            }
            var revRow = new Border
            {
                Height = PanelRigMap.RevRowHeight,
                Padding = new Thickness(PanelRigMap.RevPadX - PanelRigMap.RevGap / 2, PanelRigMap.RevPadY, PanelRigMap.RevPadX - PanelRigMap.RevGap / 2, PanelRigMap.RevPadY),
                Background = Ui.Brush(Theme.SurfaceBase),
                Margin = new Thickness(0, 0, 0, PanelRigMap.ScreenGap),
                Child = segments,
            };
            DockPanel.SetDock(revRow, Dock.Top);
            dock.Children.Add(revRow);

            var band = RigBand(PanelEmulation.Band(scenario), PanelRigMap.FaceBandIdle(screen), PanelRigMap.BandHeight(tile.Height));
            band.Margin = new Thickness(0, PanelRigMap.ScreenGap, 0, 0);
            DockPanel.SetDock(band, Dock.Bottom);
            dock.Children.Add(band);

            var column = PanelRigMap.FaceColumn(screen);
            var zones = new Grid();
            var list = PanelRigMap.FaceZones(screen);
            for (var i = 0; i < list.Count; i++)
            {
                var zone = list[i];
                var length = new GridLength(zone.Weight, GridUnitType.Star);
                if (column) zones.RowDefinitions.Add(new RowDefinition { Height = length });
                else zones.ColumnDefinitions.Add(new ColumnDefinition { Width = length });
                TextBlock text;
                if (zone.Gear)
                {
                    text = Ui.Text(zone.Text, PanelRigMap.FaceGearSize(tile.Height, column), FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
                }
                else
                {
                    text = Ui.Text(zone.Text, PanelRigMap.ZoneTextSize, FontWeights.Normal, Theme.TextSecondary);
                    text.TextTrimming = TextTrimming.CharacterEllipsis;
                }
                text.HorizontalAlignment = HorizontalAlignment.Center;
                text.VerticalAlignment = VerticalAlignment.Center;
                var cell = new Border
                {
                    Background = Ui.Brush(Theme.SurfaceZone),
                    Margin = column ? new Thickness(0, i == 0 ? 0 : PanelRigMap.ScreenGap, 0, 0) : new Thickness(i == 0 ? 0 : PanelRigMap.ScreenGap, 0, 0, 0),
                    ClipToBounds = true,
                    Child = text,
                };
                if (column) Grid.SetRow(cell, i);
                else Grid.SetColumn(cell, i);
                zones.Children.Add(cell);
            }
            dock.Children.Add(zones);

            return RigScreenFrame(tile, dock);
        }

        /// <summary>A pit wall: its band on top, and the panels of the page it is on, each named.</summary>
        private FrameworkElement RigPitWall(RigTile tile, ScreenInstance screen, string scenario)
        {
            var dock = new DockPanel { LastChildFill = true };
            var band = RigBand(PanelEmulation.Band(scenario), PanelRigMap.PitWallBandIdle(screen), PanelRigMap.PitWallBandHeight);
            band.Margin = new Thickness(0, 0, 0, PanelRigMap.ScreenGap);
            DockPanel.SetDock(band, Dock.Top);
            dock.Children.Add(band);

            var inner = 2 * (PanelRigMap.ScreenPadding + PanelMetrics.BorderWeight);
            var bodyWidth = tile.Width - inner;
            var bodyHeight = tile.Height - inner - PanelRigMap.PitWallBandHeight - PanelRigMap.ScreenGap;
            var body = new Canvas { Width = bodyWidth, Height = bodyHeight, ClipToBounds = true };
            foreach (var cell in PanelRigMap.PitWallCells(screen))
            {
                var text = Ui.Text(cell.Text, PanelRigMap.ZoneTextSize, FontWeights.Normal, Theme.TextSecondary);
                text.TextTrimming = TextTrimming.CharacterEllipsis;
                var box = new Border
                {
                    Width = Math.Max(0, Math.Floor(cell.Width * bodyWidth)),
                    Height = Math.Max(0, Math.Floor(cell.Height * bodyHeight)),
                    Padding = new Thickness(PanelRigMap.PitWallCellPadding),
                    Background = Ui.Brush(Theme.SurfaceZone),
                    Child = text,
                };
                Canvas.SetLeft(box, Math.Floor(cell.X * bodyWidth));
                Canvas.SetTop(box, Math.Floor(cell.Y * bodyHeight));
                body.Children.Add(box);
            }
            dock.Children.Add(body);
            return RigScreenFrame(tile, dock);
        }

        /// <summary>A phone: the module it opens on, or the flag over the whole of it.</summary>
        private static FrameworkElement RigCompanion(RigTile tile, ScreenInstance screen, string scenario)
        {
            var band = PanelRigMap.CompanionBand(scenario);
            var words = band.Alert ? band.Text : PanelRigMap.CompanionIdle(screen);
            var ink = band.Alert ? band.TextHex : Theme.TextSecondary;
            Brush ground = Ui.Brush(Theme.SurfaceInset);
            if (band.Chequer) ground = RigChequer;
            else if (band.Outlined) ground = Ui.Brush(Theme.SurfaceBase);
            else if (band.Alert) ground = Ui.Brush(band.FillHex);
            var text = Ui.Text(words ?? string.Empty, PanelRigMap.CompanionTextSize, FontWeights.SemiBold, ink);
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(PanelRigMap.ScreenPadding);
            return new Border
            {
                Width = tile.Width,
                Height = tile.Height,
                CornerRadius = new CornerRadius(PanelRigMap.CompanionRadius),
                Background = ground,
                BorderBrush = Ui.Brush(band.Outlined ? band.FillHex : Theme.Border),
                BorderThickness = new Thickness(band.Outlined ? PanelRigMap.CompanionOutline : PanelMetrics.BorderWeight),
                Child = text,
            };
        }

        /// <summary>A round face: its ring in the revs' colour, and the gear inside it.</summary>
        private FrameworkElement RigRound(RigTile tile, string scenario)
        {
            var grid = new Grid { Width = tile.Width, Height = tile.Height };
            grid.Children.Add(new Ellipse
            {
                Fill = Ui.Brush(Theme.SurfaceInset),
                Stroke = Ui.Brush(PanelRigMap.RingColour(scenario, Settings.RevBarMode())),
                StrokeThickness = PanelRigMap.RingThickness,
            });
            var gear = Ui.Text(PanelEmulation.Gear, PanelRigMap.RoundGearSize, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
            gear.HorizontalAlignment = HorizontalAlignment.Center;
            gear.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(gear);
            return grid;
        }

        /// <summary>A band: the flag's or the pit lane's colour and words when a scenario takes it, an outline
        /// for the black flag, the chequer with no words, and otherwise the page it is on.</summary>
        private static Border RigBand(FaceBand band, string idle, double height)
        {
            var box = new Border { Height = height, ClipToBounds = true };
            if (band.Chequer)
            {
                box.Background = RigChequer;
                return box;
            }
            string words, ink;
            if (band.Alert)
            {
                words = band.Text ?? string.Empty;
                ink = band.TextHex;
                if (band.Outlined)
                {
                    box.Background = Ui.Brush(Theme.SurfaceBase);
                    box.BorderBrush = Ui.Brush(band.FillHex);
                    box.BorderThickness = new Thickness(PanelRigMap.BandOutline);
                }
                else box.Background = Ui.Brush(band.FillHex);
            }
            else
            {
                words = idle ?? string.Empty;
                ink = Theme.TextSecondary;
                box.Background = Ui.Brush(Theme.SurfaceZone);
            }
            var text = Ui.Tracked(words, PanelRigMap.BandTextSize, FontWeights.SemiBold, ink, PanelRigMap.BandTracking);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            box.Child = text;
            return box;
        }

        /// <summary>The chequered flag: squares of the base ground and the flag's white.</summary>
        private static Brush RigChequer
        {
            get { return rigChequer ?? (rigChequer = RigChequerBrush()); }
        }

        private static Brush rigChequer;

        private static Brush RigChequerBrush()
        {
            var square = PanelRigMap.ChequerSquare;
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(Ui.Brush(Theme.SurfaceBase), null, new RectangleGeometry(new Rect(0, 0, 2 * square, 2 * square))));
            group.Children.Add(new GeometryDrawing(Ui.Brush(Theme.FlagChequer), null, new RectangleGeometry(new Rect(square, 0, square, square))));
            group.Children.Add(new GeometryDrawing(Ui.Brush(Theme.FlagChequer), null, new RectangleGeometry(new Rect(0, square, square, square))));
            var brush = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 2 * square, 2 * square),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 2 * square, 2 * square),
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None,
            };
            brush.Freeze();
            return brush;
        }

        /// <summary>The chips, in PanelEmulation's five groups; picking one repaints every tile's picture with
        /// it in place, and nothing else on the page.</summary>
        private FrameworkElement BuildRigScenarios(IList<RigTileView> views)
        {
            var host = new Border();
            RigDrawChips(host, views, null);
            return host;
        }

        private void RigDrawChips(Border host, IList<RigTileView> views, string focus)
        {
            var current = rigScenario;
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            Button focusChip = null;
            foreach (var group in PanelEmulation.Groups)
            {
                var chips = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var scenario in group.Scenarios)
                {
                    var id = scenario.Id;
                    var chip = Ui.SwatchChip(scenario.Label, scenario.SwatchHex, id == current, () => RigPick(host, views, id));
                    chip.Margin = new Thickness(0, 0, PanelRigMap.ChipGap, PanelRigMap.ChipGap);
                    if (id == focus) focusChip = chip;
                    chips.Children.Add(chip);
                }
                var title = Ui.Eyebrow(group.Title);
                title.Margin = new Thickness(0, 0, 0, PanelRigMap.GroupTitleGap);
                var column = Ui.VStack(0, title, chips);
                // The chips' own 6 under them is part of the 18 between two rows of groups.
                column.Margin = new Thickness(0, 0, PanelRigMap.GroupGapX, PanelRigMap.GroupGapY - PanelRigMap.ChipGap);
                wrap.Children.Add(column);
            }
            host.Child = wrap;
            // A chip picked from the keyboard keeps the focus through its redraw.
            if (focusChip != null)
            {
                var chip = focusChip;
                chip.Dispatcher.BeginInvoke(new Action(() => chip.Focus()), DispatcherPriority.Input);
            }
        }

        /// <summary>Paints every tile with a scenario and presses its chip. The selection outlives a rebuild,
        /// so a night-mode change or a resize keeps it.</summary>
        private void RigPick(Border host, IList<RigTileView> views, string id)
        {
            Select(PanelPage.Rig, id);
            foreach (var view in views) view.Paint(id);
            RigDrawChips(host, views, id);
        }
    }
}
