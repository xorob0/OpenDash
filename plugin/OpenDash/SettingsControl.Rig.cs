// SettingsControl.Rig.cs: the Rig page -- every screen, strip and matrix as a tile on a dotted ground the
// driver drags into the shape of the rig, and a row of chips that paints a flag, a car alongside, the pit
// lane, a warning or the revs on every tile at once.
//
// "Zone C" and "matrix 2" mean nothing until you see where they are, and the only way to check a flag used to
// be to own the hardware and wait for one (#503). Rig.dc.html is the artboard. Every size, word and rule is
// PanelRigMap's, where PanelRigMapTests holds it; this file only draws. A drop keeps every tile where it is
// drawn (ScreenInstance's, LedBar's or the matrix slot's LayoutX and LayoutY), so the rig is one arrangement;
// before the first, PanelRigMap.DefaultLayout places them. A chip repaints the tiles' pictures in place and
// rebuilds nothing.
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
        /// <remarks>
        /// Only while it is in the hand: Panel.ZIndex reorders the canvas's visual children as well as its
        /// drawing, and keyboard navigation and the shell's FocusPath walk the visual children. A tile left
        /// raised moved to the end of the Tab order, and after a rebuild focus was restored by its raised
        /// index to another tile, which the next arrow key moved and saved. RigLower puts it back.
        /// </remarks>
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

        /// <summary>The width and height the tiles were last laid out at, which a drag and a drop are held to,
        /// so the place a tile is dropped at is the place the next build draws it at.</summary>
        private sealed class RigExtent
        {
            public double Width { get; set; }
            public double Height { get; set; }
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
            var plan = PanelRigMap.Plan(Settings, width);
            var extent = new RigExtent { Width = width, Height = plan.Height };
            var canvas = new Canvas { Height = plan.Height, ClipToBounds = true, Background = Ui.DotGrid() };
            var tiles = plan.Tiles;

            if (tiles.Count == 0)
            {
                var screensPress = Ui.Button(PanelAttention.Open(PanelScreens.Title), PanelButtonKind.Outline, PanelButtonSize.Small);
                screensPress.Click += (sender, args) => Go(PanelPage.Screens);
                var empty = Ui.HStack(PanelRigMap.EmptyGap, Ui.Prose(PanelRigMap.Empty, Theme.SizeBody), screensPress);
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
                var view = BuildRigTile(tile, extent, scenario, views);
                views.Add(view);
                canvas.Children.Add(view.Element);
            }

            // ContentWidth allows for a scroll bar the page may not be showing, so the canvas can be wider than
            // the width the plan was worked out for. Laid out again at the width it really has, the default is
            // centred in it and a drag is held to the same edge the next build clamps to.
            canvas.SizeChanged += (sender, args) =>
            {
                var real = canvas.ActualWidth;
                if (real <= 0 || Math.Abs(real - extent.Width) < 0.5) return;
                var again = PanelRigMap.Plan(Settings, real);
                extent.Width = real;
                extent.Height = again.Height;
                canvas.Height = again.Height;
                foreach (var view in views)
                {
                    var moved = again.Tiles.FirstOrDefault(t => t.Id == view.Tile.Id);
                    if (moved == null) continue;
                    Canvas.SetLeft(view.Element, moved.X);
                    Canvas.SetTop(view.Element, moved.Y);
                }
            };

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
        private RigTileView BuildRigTile(RigTile tile, RigExtent extent, string scenario, IList<RigTileView> views)
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
            if (tile.Kind == RigTileKind.Face)
            {
                // A wheel button pages a face's zones without a save or a rebuild, so the clock looks.
                var seen = PanelRigMap.FaceState(Settings.ScreenByNamespace(tile.Key));
                OnTick(() =>
                {
                    var now = PanelRigMap.FaceState(Settings.ScreenByNamespace(tile.Key));
                    if (now == seen) return;
                    seen = now;
                    paint(rigScenario);
                });
            }

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
                // The whole name, where the line above the picture trims it, and what the dot means; the Thumb
                // is what the pointer is on, the dot included.
                ToolTip = PanelRigMap.TileLabel(tile, warns),
            };
            AutomationProperties.SetName(thumb, PanelRigMap.TileLabel(tile, warns));

            var root = new Grid { Width = PanelRigMap.FootprintWidth(tile), Height = PanelRigMap.FootprintHeight(tile) };
            root.Children.Add(body);
            root.Children.Add(thumb);
            Canvas.SetLeft(root, tile.X);
            Canvas.SetTop(root, tile.Y);
            // The Thumb is focusable for the arrow keys, so a press focuses it, and focus brings a tile into view
            // after the Thumb has taken its grip: a tile half out of view would scroll the page and jump that far
            // on the first move. Only the keyboard's focus scrolls to a tile.
            root.RequestBringIntoView += (sender, args) =>
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed) args.Handled = true;
            };

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
                Canvas.SetLeft(root, PanelRigMap.Clamp(Canvas.GetLeft(root) + args.HorizontalChange, root.Width, extent.Width));
                Canvas.SetTop(root, PanelRigMap.Clamp(Canvas.GetTop(root) + args.VerticalChange, root.Height, extent.Height));
            };
            // Saved on the drop, never in End(): SimHub is force-killed on the VM.
            thumb.DragCompleted += (sender, args) =>
            {
                RigLower(root);
                if (!moved) return;
                RigPlace(root, Canvas.GetLeft(root), Canvas.GetTop(root), extent);
                RigDrop(tile, views);
            };
            // An arrow key moves the tile a step; a held key repeats the move, and the tile is saved once, when
            // the key comes up (or focus leaves first), rather than on every repeat.
            var unsaved = false;
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
                if (!RigPlace(root, Canvas.GetLeft(root) + dx, Canvas.GetTop(root) + dy, extent)) return;
                unsaved = true;
                // WPF does not scroll to a focused element that moves; the left button is up, so the guard
                // above lets this through.
                root.BringIntoView();
            };
            thumb.KeyUp += (sender, args) =>
            {
                RigLower(root);
                if (!unsaved) return;
                unsaved = false;
                RigDrop(tile, views);
            };
            thumb.LostKeyboardFocus += (sender, args) =>
            {
                RigLower(root);
                if (!unsaved) return;
                unsaved = false;
                RigDrop(tile, views);
            };

            return new RigTileView(tile, root, paint);
        }

        /// <summary>Puts a tile back in the canvas's order once it is out of the hand, so Tab and the shell's
        /// focus restore walk the tiles in the order they were built. A tile it is over may now draw over it,
        /// as the next build draws them anyway.</summary>
        private static void RigLower(FrameworkElement root)
        {
            root.ClearValue(Panel.ZIndexProperty);
        }

        /// <summary>Puts a tile on the nearest step of the grid inside the canvas, the place it will be kept at.
        /// Returns whether that moved it.</summary>
        private static bool RigPlace(FrameworkElement root, double x, double y, RigExtent extent)
        {
            var left = PanelRigMap.DropPosition(x, root.Width, extent.Width);
            var top = PanelRigMap.DropPosition(y, root.Height, extent.Height);
            var moved = left != Canvas.GetLeft(root) || top != Canvas.GetTop(root);
            Canvas.SetLeft(root, left);
            Canvas.SetTop(root, top);
            return moved;
        }

        /// <summary>
        /// Keeps every tile where it is drawn, the dropped one included, so the rig is kept as one
        /// arrangement (PanelRigMap.Plan). Arranging a screen is setting it up, so the dropped tile's screen
        /// is saved with Save(screen), which keeps a migrated screen as Home's list asks; that answers Home's
        /// unclaimed screens, so the first keep asks again what needs fixing, and the Screens item's dot and
        /// Home's count follow it. An ordinary drop asks SimHub nothing.
        /// </summary>
        private void RigDrop(RigTile tile, IList<RigTileView> views)
        {
            PanelRigMap.SavePlaces(Settings, views.Select(view => view.Tile.At(Canvas.GetLeft(view.Element), Canvas.GetTop(view.Element))));
            var screen = PanelRigMap.ScreenOf(Settings, tile);
            var claiming = screen != null && screen.Unclaimed == true;
            if (screen != null) Save(screen);
            else Save();
            if (claiming)
            {
                RefreshAttention();
                RefreshSidebar();
            }
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
                    var options = PanelRigMap.MatrixOptionsFor(Settings, PanelRigMap.MatrixSlot(tile), scenario);
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
        /// the dash's own words for the scenario; a flag goes on the band or over the zones by the face's own
        /// flag format.
        /// </summary>
        private FrameworkElement RigFace(RigTile tile, ScreenInstance screen, string scenario)
        {
            var dock = new DockPanel { LastChildFill = true };

            var revs = PanelRigMap.FaceRevs(scenario, Settings, screen);
            var segments = new UniformGrid { Rows = 1, Columns = revs.Length };
            foreach (var led in revs)
            {
                segments.Children.Add(new Border
                {
                    Margin = new Thickness(PanelRigMap.RevGap / 2, 0, PanelRigMap.RevGap / 2, 0),
                    CornerRadius = new CornerRadius(PanelRigMap.RevRadius),
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

            var format = PanelRigMap.FaceFlagFormat(Settings, screen);
            var inner = PanelRigMap.ScreenInner(tile.Width);
            var band = RigBand(PanelRigMap.BandPaint(PanelRigMap.FaceBandFor(scenario, format), inner));
            band.Height = PanelRigMap.BandHeight(tile.Height);
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
            dock.Children.Add(RigCovered(zones, PanelRigMap.FaceBlockFor(scenario, format), inner));

            return RigScreenFrame(tile, dock);
        }

        /// <summary>A screen's body, and over it the flag's block when the screen draws its flags full screen.</summary>
        private static UIElement RigCovered(UIElement body, FaceBand block, double width)
        {
            if (block == null || !block.Alert) return body;
            var grid = new Grid();
            grid.Children.Add(body);
            grid.Children.Add(RigBand(PanelRigMap.BandPaint(block, width)));
            return grid;
        }

        /// <summary>A pit wall: its band on top, and the panels of the page it is on, each named; a flag goes on
        /// the band or over the panels by the pit wall's own flag format, and nowhere when that is off.</summary>
        private FrameworkElement RigPitWall(RigTile tile, ScreenInstance screen, string scenario)
        {
            var dock = new DockPanel { LastChildFill = true };
            var format = PanelRigMap.PitWallFlagFormat(Settings, screen);
            var inner = PanelRigMap.ScreenInner(tile.Width);
            var band = RigBand(PanelRigMap.BandPaint(PanelRigMap.PitWallBandFor(scenario, format), inner));
            band.Height = PanelRigMap.PitWallBandHeight;
            band.Margin = new Thickness(0, 0, 0, PanelRigMap.ScreenGap);
            DockPanel.SetDock(band, Dock.Top);
            dock.Children.Add(band);

            var bodyWidth = inner;
            var bodyHeight = tile.Height - 2 * (PanelRigMap.ScreenPadding + PanelMetrics.BorderWeight) - PanelRigMap.PitWallBandHeight - PanelRigMap.ScreenGap;
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
            dock.Children.Add(RigCovered(body, PanelRigMap.PitWallBlockFor(scenario, format), inner));
            return RigScreenFrame(tile, dock);
        }

        /// <summary>A phone: the module it opens on, the flag over the whole of it, or the flag's strip at its
        /// foot, by the phone's own flag format.</summary>
        private FrameworkElement RigCompanion(RigTile tile, ScreenInstance screen, string scenario)
        {
            var format = PanelRigMap.CompanionFlagFormat(Settings, screen);
            var paint = PanelRigMap.CompanionPaint(PanelRigMap.CompanionBand(scenario, format), PanelRigMap.CompanionIdle(screen));
            var phone = RigPainted(paint, PanelRigMap.CompanionTextSize, PanelRigMap.CompanionTracking);
            phone.Width = tile.Width;
            phone.Height = tile.Height;
            phone.CornerRadius = new CornerRadius(PanelRigMap.CompanionRadius);
            var words = phone.Child as FrameworkElement;
            if (words != null) words.Margin = new Thickness(PanelRigMap.ScreenPadding);

            var strip = PanelRigMap.StripPaint(PanelRigMap.CompanionStrip(scenario, format));
            if (strip != null)
            {
                var bar = RigBand(strip);
                bar.Height = PanelRigMap.CompanionStripHeight;
                bar.Margin = new Thickness(PanelRigMap.ScreenPadding, 0, PanelRigMap.ScreenPadding, PanelRigMap.ScreenPadding);
                var dock = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(bar, Dock.Bottom);
                dock.Children.Add(bar);
                phone.Child = null;
                if (words != null) dock.Children.Add(words);
                phone.Child = dock;
            }
            return phone;
        }

        /// <summary>A round face: its ring, in the flag's colour while a flag is picked and the revs' colour
        /// otherwise, and the gear inside it.</summary>
        private FrameworkElement RigRound(RigTile tile, string scenario)
        {
            var ring = PanelRigMap.RingFor(scenario, Settings);
            var grid = new Grid { Width = tile.Width, Height = tile.Height };
            grid.Children.Add(new Ellipse
            {
                Fill = Ui.Brush(Theme.SurfaceInset),
                Stroke = ring.Chequer ? RigChequer : Ui.Brush(ring.Hex),
                StrokeThickness = ring.Thickness,
            });
            var gear = Ui.Text(PanelEmulation.Gear, PanelRigMap.RoundGearSize, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
            gear.HorizontalAlignment = HorizontalAlignment.Center;
            gear.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(gear);
            return grid;
        }

        /// <summary>A band, a block or a strip as PanelRigMap paints it, in the band's words.</summary>
        private static Border RigBand(RigPaint paint)
        {
            return RigPainted(paint, PanelRigMap.BandTextSize, PanelRigMap.BandTracking);
        }

        /// <summary>What PanelRigMap's paint says, drawn: the ground or the chequer, the outline, and the words
        /// tracked, or wrapped where they are not.</summary>
        private static Border RigPainted(RigPaint paint, double textSize, double tracking)
        {
            var box = new Border { ClipToBounds = true };
            if (paint.Chequer) box.Background = RigChequer;
            else if (paint.FillHex != null) box.Background = Ui.Brush(paint.FillHex);
            if (paint.BorderHex != null)
            {
                box.BorderBrush = Ui.Brush(paint.BorderHex);
                box.BorderThickness = new Thickness(paint.BorderWidth);
            }
            if (string.IsNullOrEmpty(paint.Words)) return box;
            FrameworkElement text;
            if (paint.Tracked) text = Ui.Tracked(paint.Words, textSize, FontWeights.SemiBold, paint.InkHex, tracking);
            else
            {
                var block = Ui.Text(paint.Words, textSize, FontWeights.SemiBold, paint.InkHex);
                block.TextWrapping = TextWrapping.Wrap;
                block.TextAlignment = TextAlignment.Center;
                text = block;
            }
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
            AutomationProperties.SetName(host, PanelRigMap.ScenariosName);
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
