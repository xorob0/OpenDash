// PanelRigMapTests.cs: the Rig page's words and sizes, the tile each device becomes, where the tiles go before
// and after a driver drags them, and what each tile shows under a scenario.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelRigMapTests
    {
        private static RigTile Tile(RigTileKind kind, string id, double width = 100, double height = 50)
        {
            return new RigTile(kind, id, id, 0, 0, width, height);
        }

        private static ScreenInstance Screen(string kind, string ns, string name, int width, int height)
        {
            return new ScreenInstance { Kind = kind, Namespace = ns, Name = name, Width = width, Height = height };
        }

        /// <summary>A rig like the artboard's: two faces, a pit wall, a portrait phone, a round, two strips and
        /// two matrices.</summary>
        private static OpenDashSettings Rig()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            settings.Rig = new List<ScreenInstance>
            {
                Screen(Contract.KindFace, "MainDash", "Main dash", 1280, 480),
                Screen(Contract.KindFace, "Rim", "Rim", 1280, 480),
                Screen(Contract.KindPitWall, "PitWall", "Pit wall", 1920, 1080),
                Screen(Contract.KindCompanion, "Companion", "Phone", 480, 850),
                Screen(Contract.KindSlots, "Slots480x480", "Round", 480, 480),
            };
            settings.AddLedBar("0-15-0", "Dash brow", LedBar.ArduinoDevice);
            settings.AddLedBar("3-9-3", "Wheel rim", LedBar.ArduinoDevice);
            settings.AddMatrixPanel("Left pillar");
            settings.AddMatrixPanel("Flag box");
            return settings;
        }

        [Fact]
        public void The_rig_page_says_what_the_artboard_says_in_the_panels_words()
        {
            Assert.Equal("Rig", PanelRigMap.Title);
            Assert.Equal("Drag to arrange like your rig", PanelRigMap.CanvasHint);
            Assert.Equal("Reset layout", PanelRigMap.ResetLayout);
            Assert.Equal("Nothing on the rig yet.", PanelRigMap.Empty);
            // "Light the real hardware" on the artboard: a switch names the thing.
            Assert.Equal("Real hardware", PanelRigMap.RealHardwareTitle);
            Assert.Same(PanelSoon.RealHardware, PanelSoon.Find(PanelRigMap.RealHardwareTitle));
            Assert.Equal(506, PanelSoon.RealHardware.Ticket);
            Assert.Equal(new[] { PanelSoon.RealHardware }, PanelRigMap.SoonDrawn);
            Assert.Equal("Leaderboard", PanelRigMap.BoardLabel);
            // The artboard's <section aria-label="What to emulate"> is a wh-clause posing as a heading; a
            // heading is a noun (voice.md), and the one noun for the chips on every page is "Preview" (#524,
            // ruling 6). The name a screen reader announces is one search finds, and "emulation" still does.
            Assert.Equal("Preview", PanelRigMap.ScenariosName);
            Assert.Contains(PanelSearch.Find(PanelSearch.All(), PanelRigMap.ScenariosName), hit => hit.Route.Page == PanelPage.Rig);
            Assert.All(PanelSearch.Find(PanelSearch.All(), "emulation"), hit => Assert.Equal(PanelPage.Rig, hit.Route.Page));
            Assert.DoesNotContain(new[] { "What", "Where", "How", "Which", "When", "Who", "Why" }, w => PanelRigMap.ScenariosName.StartsWith(w + " ", StringComparison.Ordinal));
            // The chips are the voice's words: "Pit limiter" and the temperatures by name.
            Assert.Equal(new[] { "Flags", "Spotter", "Pit lane", "Warnings", "Revs" }, PanelEmulation.Groups.Select(g => g.Title));
            Assert.Equal(new[] { "Green", "Yellow", "Blue", "White", "Black", "Chequered", "Red", "Car left", "Car right", "Both sides", "Pit limiter", "Speeding", "Low fuel", "Oil temperature", "Water temperature", "Idle", "Mid revs", "Shift point" },
                PanelEmulation.Scenarios().Select(s => s.Label));
        }

        [Fact]
        public void The_rig_page_is_drawn_at_the_artboards_sizes()
        {
            Assert.Equal(580, PanelRigMap.CanvasHeight);
            Assert.Equal(20, PanelRigMap.GridStep);
            Assert.Equal(12, PanelRigMap.TitleTagGap);
            Assert.Equal(18, PanelRigMap.HeaderGap);
            Assert.Equal(10, PanelRigMap.HeaderLabelGap);
            Assert.Equal(12, PanelRigMap.HeaderStackGap);
            Assert.Equal(30, PanelRigMap.ResetButtonHeight);
            Assert.Equal(12, PanelRigMap.ResetButtonPaddingX);
            Assert.Equal(14, PanelRigMap.HintLeft);
            Assert.Equal(12, PanelRigMap.HintBottom);
            Assert.Equal(16, PanelRigMap.NameHeight);
            Assert.Equal(6, PanelRigMap.NameGap);
            Assert.Equal(12, PanelRigMap.NameSize);
            Assert.Equal(6, PanelRigMap.WarnDot);
            Assert.Equal(6, PanelRigMap.WarnGap);
            Assert.Equal(300, PanelRigMap.FaceWidth);
            Assert.Equal(180, PanelRigMap.FaceMaxHeight);
            Assert.Equal(240, PanelRigMap.PitWallWidth);
            Assert.Equal(135, PanelRigMap.PitWallHeight);
            Assert.Equal(76, PanelRigMap.CompanionWidth);
            Assert.Equal(135, PanelRigMap.CompanionHeight);
            Assert.Equal(110, PanelRigMap.RoundSize);
            Assert.Equal(4, PanelRigMap.ScreenPadding);
            Assert.Equal(3, PanelRigMap.ScreenGap);
            Assert.Equal(3, PanelRigMap.ScreenRadius);
            Assert.Equal(20, PanelRigMap.RevSegments);
            Assert.Equal(1, PanelRigMap.RevRadius);
            Assert.Equal(12, PanelRigMap.EmptyGap);
            Assert.Equal(44, PanelRigMap.HintClear);
            Assert.Equal(8, PanelRigMap.CompanionStripHeight);
            Assert.Equal(2, PanelRigMap.RingOutline);
            // The segments are the artboard's 9; its row is content-box, so the padding is outside them.
            Assert.Equal(9, PanelRigMap.RevSegmentHeight);
            Assert.Equal(11, PanelRigMap.RevRowHeight);
            Assert.Equal(2, PanelRigMap.RevGap);
            Assert.Equal(2, PanelRigMap.RevPadX);
            Assert.Equal(1, PanelRigMap.RevPadY);
            Assert.Equal(10, PanelRigMap.ZoneTextSize);
            Assert.Equal(10, PanelRigMap.BandTextSize);
            Assert.Equal(0.14, PanelRigMap.BandTracking);
            Assert.Equal(0.18, PanelRigMap.BandRatio);
            Assert.Equal(12, PanelRigMap.BandMinHeight);
            Assert.Equal(2, PanelRigMap.BandOutline);
            Assert.Equal(3, PanelRigMap.CompanionOutline);
            Assert.Equal(5, PanelRigMap.ChequerSquare);
            Assert.Equal(16, PanelRigMap.PitWallBandHeight);
            Assert.Equal(4, PanelRigMap.PitWallCellPadding);
            Assert.Equal(6, PanelRigMap.CompanionRadius);
            Assert.Equal(9, PanelRigMap.CompanionTextSize);
            Assert.Equal(0.12, PanelRigMap.CompanionTracking);
            Assert.Equal(6, PanelRigMap.RingThickness);
            Assert.Equal(44, PanelRigMap.RoundGearSize);
            Assert.Equal(32, PanelRigMap.GroupGapX);
            Assert.Equal(18, PanelRigMap.GroupGapY);
            Assert.Equal(8, PanelRigMap.GroupTitleGap);
            Assert.Equal(6, PanelRigMap.ChipGap);
            Assert.Equal(24, PanelRigMap.LayoutMargin);
            Assert.Equal(28, PanelRigMap.LayoutGap);
            Assert.Equal(8, PanelRigMap.LayoutGapMin);
            // The dash's portrait page (screens/pitwall.ts): the board 800 of the 1856 px body, the zones
            // from 912, and the gutter between them.
            Assert.Equal(0.43, PanelRigMap.PortraitBoardShare);
            Assert.Equal(0.49, PanelRigMap.PortraitZonesTop);
            Assert.Equal(0.02, PanelRigMap.PortraitCellGap);
            Assert.Equal(PanelRigMap.PortraitBoardShare, Math.Round(800.0 / 1856, 2));
            Assert.Equal(PanelRigMap.PortraitZonesTop, Math.Round(912.0 / 1856, 2));
        }

        [Fact]
        public void Search_finds_the_header_and_every_group_of_chips_on_the_rig_page()
        {
            Assert.Equal(new[] { "Rig", "Night mode", "Reset layout", "Flags", "Spotter", "Pit lane", "Warnings", "Revs" }, PanelRigMap.Search.Select(e => e.Label));
            Assert.All(PanelRigMap.Search, e => Assert.Equal(PanelPage.Rig, e.Route.Page));
            Assert.Equal(PanelRigMap.AnchorCanvas, PanelRigMap.Search[0].Route.Anchor);
            // Night mode and Reset layout are in the header, at the page's top.
            Assert.Null(PanelRigMap.Search[1].Route.Anchor);
            Assert.Null(PanelRigMap.Search[2].Route.Anchor);
            Assert.All(PanelRigMap.Search.Skip(3), e => Assert.Equal(PanelRigMap.AnchorScenarios, e.Route.Anchor));
            Assert.Equal(PanelSettings.NightModeTitle, PanelRigMap.Search[1].Label);
            Assert.DoesNotContain(PanelRigMap.Search, e => e.Label == PanelRigMap.RealHardwareTitle);
            // The words a driver types for each, which a search that finds the label alone would lose. A
            // keyword is matched when it contains the query, so "emulation" is carried beside "emulate".
            Assert.Equal(new[] { "rig layout", "map", "arrange", "tiles", "emulate", "emulation" }, PanelRigMap.Search[0].Keywords);
            Assert.Equal(new[] { "dark", "dim" }, PanelRigMap.Search[1].Keywords);
            Assert.Equal(new[] { "arrange", "tiles", "rig layout" }, PanelRigMap.Search[2].Keywords);
            Assert.Equal(new[] { "emulate", "emulation", "preview", "test", "yellow", "blue", "chequered" }, PanelRigMap.Search[3].Keywords);
            Assert.Equal(new[] { "emulate", "emulation", "preview", "car left", "car right" }, PanelRigMap.Search[4].Keywords);
            Assert.Equal(new[] { "emulate", "emulation", "preview", "limiter", "speeding" }, PanelRigMap.Search[5].Keywords);
            Assert.Equal(new[] { "emulate", "emulation", "preview", "fuel", "oil", "water" }, PanelRigMap.Search[6].Keywords);
            Assert.Equal(new[] { "emulate", "emulation", "preview", "shift point", "rpm" }, PanelRigMap.Search[7].Keywords);
        }

        /// <summary>The body of one method of the Rig page, comments stripped, up to its closing brace.</summary>
        private static string RigMethod(string signature)
        {
            var page = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Rig.cs"));
            var start = page.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, signature);
            var end = page.IndexOf("\n        }\n", start, StringComparison.Ordinal);
            Assert.True(end > start, signature);
            return page.Substring(start, end - start);
        }

        /// <summary>One handler of a method, from where it is attached to the first "};" after it, so a pin on
        /// it cannot be met by the handler below it.</summary>
        private static string Handler(string method, string attach)
        {
            var start = method.IndexOf(attach, StringComparison.Ordinal);
            Assert.True(start >= 0, attach);
            var end = method.IndexOf("};", start, StringComparison.Ordinal);
            Assert.True(end > start, attach);
            return method.Substring(start, end - start);
        }

        [Fact]
        public void A_tile_is_raised_only_while_it_is_in_the_hand_and_a_key_brings_it_into_view()
        {
            var tile = RigMethod("private RigTileView BuildRigTile(");
            // Raised on the press and the key, and lowered on the drop and the key coming up or focus leaving,
            // so Tab and the shell's focus restore walk the tiles in their order.
            Assert.Contains("Panel.SetZIndex(root, ++rigTopZ);", Handler(tile, "thumb.DragStarted +="));
            InOrder(Handler(tile, "thumb.DragCompleted +="), "RigLower(root);", "if (!moved) return;");
            InOrder(Handler(tile, "thumb.KeyUp +="), "RigLower(root);", "if (extent.Pending != tile) return;");
            InOrder(Handler(tile, "thumb.LostKeyboardFocus +="), "RigLower(root);", "if (extent.Pending != tile) return;");
            Assert.Contains("root.ClearValue(Panel.ZIndexProperty);", RigMethod("private static void RigLower("));
            // A tile an arrow key moves is scrolled to, since WPF does not follow a focused element that moves;
            // the key never raises it, so a rebuild while it is held restores focus to the same tile.
            InOrder(Handler(tile, "thumb.KeyDown +="), "RigPlace(root,", "extent.Pending = tile;", "root.BringIntoView(RigRingBounds(root, extent));");
            InOrder(RigMethod("private static Rect RigRingBounds("), "(Theme.FocusRingOffset + Theme.FocusRing) / (extent.Scale > 0 ? extent.Scale : 1)", "new Rect(-outset, -outset, root.Width + 2 * outset, root.Height + 2 * outset)");
            Assert.DoesNotContain("SetZIndex", Handler(tile, "thumb.KeyDown +="));
            Assert.Equal(1, tile.Split("Panel.SetZIndex(").Length - 1);

            // A rebuild that lands mid-drag gives the focus back to the tile in the hand rather than to the
            // one the shell's restore finds by the raised tile's place, and after that restore (Loaded).
            InOrder(Handler(tile, "thumb.DragStarted +="), "Panel.SetZIndex(root, ++rigTopZ);", "rigHeld = tile.Id;");
            InOrder(Handler(tile, "thumb.DragCompleted +="), "RigLower(root);", "rigHeld = null;", "if (!extent.Live) return;");
            InOrder(tile, "AutomationProperties.SetName(thumb,", "if (rigHeld == tile.Id)", "rigHeld = null;", "Keyboard.Focus(thumb)", "DispatcherPriority.Input);", "thumb.DragStarted +=");
            Assert.Equal(1, tile.Split("rigHeld = tile.Id;").Length - 1);
        }

        private static void InOrder(string text, params string[] parts)
        {
            var at = 0;
            foreach (var part in parts)
            {
                var found = text.IndexOf(part, at, StringComparison.Ordinal);
                Assert.True(found >= 0, "missing, or out of order: " + part);
                at = found + part.Length;
            }
        }

        /// <remarks>
        /// The page's writes, held as text as the other pages hold theirs: a drop that is never saved, or a
        /// Reset layout that writes nothing, would otherwise pass every check. A drop is saved on the drop
        /// rather than in End(), since SimHub is force-killed on the VM; a screen is saved with Save(screen),
        /// which keeps a migrated screen; and a chip repaints in place and rebuilds nothing. Each handler is
        /// cut out before it is searched, so the handler below it cannot meet its pin.
        /// </remarks>
        [Fact]
        public void The_rig_page_keeps_a_drop_and_a_reset_and_a_chip_rebuilds_nothing()
        {
            var drop = RigMethod("private void RigDrop(");
            InOrder(drop, "PanelRigMap.SavePlaces(Settings, views.Select(view => view.Tile.At(Canvas.GetLeft(view.Element), Canvas.GetTop(view.Element))));", "PanelRigMap.ScreenOf(Settings, tile)", "screen.Unclaimed == true", "Save(screen);", "Save();", "if (claiming)", "RefreshAttention();", "RefreshSidebar();");
            // An ordinary drop asks SimHub nothing: the refresh is inside the claim's braces, and only there.
            Assert.Contains("if (claiming)\n            {\n                RefreshAttention();\n                RefreshSidebar();\n            }", drop);
            Assert.Equal(1, drop.Split("RefreshAttention();").Length - 1);
            Assert.Equal(1, drop.Split("RefreshSidebar();").Length - 1);
            InOrder(RigMethod("private void RigResetLayout("), "PanelRigMap.ClearLayout(Settings);", "Save();", "Redraw();");
            // The canvas is planned once, at the page's width less its frame, and drawn from that plan: the
            // tiles' room, which a drag is held to, and the canvas as high as the room at the plan's scale.
            // Nothing plans it again at the width it has once laid out, which switched layouts and heights as
            // the scroll bar came and went.
            var canvas = RigMethod("private FrameworkElement BuildRigCanvas(");
            InOrder(canvas,
                "var width = Math.Max(0, ContentWidth - 2 * PanelMetrics.BorderWeight);",
                "var plan = PanelRigMap.Plan(Settings, width);",
                "var extent = new RigExtent { Width = plan.Width, Height = plan.Height, Scale = plan.Scale, Live = true };",
                "var dots = RigDots(plan.Scale);",
                "var canvas = new Canvas { Width = plan.DrawnWidth, Height = plan.Height * plan.Scale, ClipToBounds = true };",
                "var layer = new Canvas { Width = plan.Width, Height = plan.Height, RenderTransform = new ScaleTransform(plan.Scale, plan.Scale) };",
                "var tiles = plan.Tiles;",
                "canvas.Children.Add(layer);",
                "var scenario = rigScenario;",
                "foreach (var tile in tiles)",
                "var view = BuildRigTile(tile, extent, scenario, views);",
                "views.Add(view);",
                "layer.Children.Add(view.Element);",
                // Let go of after the tiles are built, a pending arrow move saved before the build is dead.
                "OnDrop(() =>",
                "var pending = extent.Pending;",
                "extent.Pending = null;",
                "if (pending != null) RigDrop(pending, views);",
                "extent.Live = false;",
                // The frame is the plan's, and a room wider than the canvas scrolls across inside it.
                "var frame = new Grid { Height = plan.DrawnHeight, ClipToBounds = true };",
                "BorderBrush = Ui.Brush(Theme.Rule),",
                "Child = frame,",
                "canvas.HorizontalAlignment = HorizontalAlignment.Left;",
                "canvas.VerticalAlignment = VerticalAlignment.Top;",
                "if (plan.Scrolls(width))",
                // Held off the scroller's clip by the focus ring's outset, so a tile at an edge keeps its ring.
                "var outset = Theme.FocusRingOffset + Theme.FocusRing;",
                "canvas.Margin = new Thickness(outset);",
                // The ground on the room, so it scrolls across with it.
                "canvas.Background = dots;",
                "var scroller = new RigScroller",
                "HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,",
                "VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,",
                // No Tab stop, no focus taken by a press on the ground, no page key swallowed.
                "Focusable = false,",
                "Content = canvas,",
                "scroller.PreviewMouseWheel += RigPassWheel;",
                "RigKeepScroll(scroller, extent);",
                // The scroller fills the frame, so its bar is along the frame's foot.
                "frame.Height = plan.DrawnHeight + 2 * outset + SystemParameters.HorizontalScrollBarHeight;",
                // The hint above the bar and the outset, behind the scroller.
                "if (hint != null)",
                "hint.Margin = new Thickness(PanelRigMap.HintLeft, 0, 0, PanelRigMap.HintBottom + outset + SystemParameters.HorizontalScrollBarHeight);",
                "frame.Children.Add(hint);",
                "frame.Children.Add(scroller);",
                // A canvas that fits: the outline spans the column, flush with the header, and the ground
                // goes across the whole frame as high as the room is drawn; the hint is behind the room.
                "frame.Children.Add(new Border { Height = canvas.Height, VerticalAlignment = VerticalAlignment.Top, Background = dots });",
                "if (hint != null) frame.Children.Add(hint);",
                "frame.Children.Add(canvas);",
                "return outline;");
            // The scroller leaves the page keys a focused tile does not take to the page: Focusable = false
            // keeps the focus off it, but not its class handler, which marked them handled on an axis it
            // cannot scroll (#527).
            Assert.DoesNotContain("new ScrollViewer", canvas);
            InOrder(RigMethod("private sealed class RigScroller : ScrollViewer"),
                "protected override void OnKeyDown(KeyEventArgs e)",
                "if (e.Key == Key.PageUp || e.Key == Key.PageDown) return;",
                "if ((e.Key == Key.Home || e.Key == Key.End) && (Keyboard.Modifiers & ModifierKeys.Control) != 0) return;",
                "base.OnKeyDown(e);");
            // The scroller fills the frame rather than standing at its top, so its bar is along the foot.
            Assert.DoesNotContain("VerticalAlignment", Handler(canvas, "var scroller = new "));
            Assert.DoesNotContain("outline.Width", canvas);
            Assert.DoesNotContain("outline.HorizontalAlignment", canvas);
            Assert.Equal(1, canvas.Split("canvas.Background = dots;").Length - 1);
            Assert.DoesNotContain("frame.Background", canvas);
            Assert.DoesNotContain("Canvas.SetBottom(hint", canvas);
            Assert.DoesNotContain("canvas.Children.Add(hint)", canvas);
            // A rebuild draws a canvas that scrolls across where it was scrolled to, and only the build that
            // is the page's keeps the place.
            InOrder(RigMethod("private void RigKeepScroll("), "var restore = rigScrollX;", "scroller.Loaded +=", "scroller.ScrollToHorizontalOffset(restore);", "scroller.ScrollChanged +=", "if (!restored || !extent.Live) return;", "rigScrollX = scroller.HorizontalOffset;");
            Assert.Equal(1, canvas.Split("OnDrop(").Length - 1);
            InOrder(RigMethod("private static void RigPassWheel("), "if (args.Handled) return;", "args.Handled = true;", "parent.RaiseEvent(", "RoutedEvent = UIElement.MouseWheelEvent");
            // The empty rig's press opens the Add sheet on Screens in the add tile's words, as Home's empty-rig
            // tile does; the hint is in the frame's lower left, wherever the room is drawn, and under the tiles.
            InOrder(canvas,
                "if (tiles.Count == 0)",
                "Ui.Button(PanelAddScreen.SectionTitle,",
                "screensPress.Click += (sender, args) => Go(PanelPage.Screens, PanelScreens.AnchorAdd);",
                "canvas.Children.Add(empty);",
                "hint = Ui.Text(PanelRigMap.CanvasHint,",
                "hint.HorizontalAlignment = HorizontalAlignment.Left;",
                "hint.VerticalAlignment = VerticalAlignment.Bottom;",
                "hint.Margin = new Thickness(PanelRigMap.HintLeft, 0, 0, PanelRigMap.HintBottom);",
                "canvas.Children.Add(layer);");
            Assert.DoesNotContain("SizeChanged", canvas);
            Assert.DoesNotContain("ActualWidth", canvas);
            Assert.Equal(1, canvas.Split("PanelRigMap.Plan(").Length - 1);
            InOrder(RigMethod("private static Brush RigDots("), "Ui.DotGrid();", "if (scale >= 1) return dots;", "scaled.Transform = new ScaleTransform(scale, scale);");
            // Reset layout is what the header's press does.
            InOrder(RigMethod("private FrameworkElement BuildRigHeader("),
                "var reset = Ui.Button(PanelRigMap.ResetLayout, PanelButtonKind.Outline, PanelButtonSize.Small);",
                "reset.Height = PanelRigMap.ResetButtonHeight;",
                "reset.Padding = new Thickness(PanelRigMap.ResetButtonPaddingX, 0, PanelRigMap.ResetButtonPaddingX, 0);",
                "reset.Click += (sender, args) => RigResetLayout();");

            var tile = RigMethod("private RigTileView BuildRigTile(");
            // A drag is held inside all four edges, and only a drag that moved saves: a plain click keeps
            // the tile following the default, and a click with a pixel of jitter is still a click until the
            // pointer has gone the system's drag distance.
            var delta = Handler(tile, "thumb.DragDelta +=");
            InOrder(delta,
                "if (!moved && Math.Abs(args.HorizontalChange) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(args.VerticalChange) < SystemParameters.MinimumVerticalDragDistance) return;",
                "moved = true;",
                "PanelRigMap.Clamp(Canvas.GetLeft(root) + args.HorizontalChange, root.Width, extent.Width)",
                "PanelRigMap.Clamp(Canvas.GetTop(root) + args.VerticalChange, root.Height, extent.Height)");
            // Nothing scrolls while the tile is in the hand: the Thumb reports the pointer against the tile,
            // so a scroll under a still pointer came back as the next move and the tile ran away (#527).
            foreach (var scroll in new[] { "BringIntoView", "ScrollTo", "Offset", "RigFollow" }) Assert.DoesNotContain(scroll, delta);
            Assert.DoesNotContain("RigFollow", tile);
            Assert.DoesNotContain("RigFollow", RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Rig.cs")));
            InOrder(Handler(tile, "thumb.DragStarted +="), "moved = false;");
            // The drop brings the tile into view with its ring, across the canvas and down the page, through
            // the guard that keeps a press from scrolling.
            InOrder(Handler(tile, "thumb.DragCompleted +="), "if (!extent.Live) return;", "if (args.Canceled)", "Canvas.SetLeft(root, startLeft);", "Canvas.SetTop(root, startTop);", "return;", "if (!moved) return;", "RigPlace(root, Canvas.GetLeft(root), Canvas.GetTop(root), extent);",
                "showing = true;", "root.BringIntoView(RigRingBounds(root, extent));", "showing = false;", "RigDrop(tile, views);");
            InOrder(Handler(tile, "thumb.DragStarted +="), "startLeft = Canvas.GetLeft(root);", "startTop = Canvas.GetTop(root);");
            // The tile is built where the plan puts it, its picture painted in the build's scenario, and its
            // view repaints it: the chips reach it through that.
            InOrder(tile,
                "var host = new Border { Width = tile.Width, Height = tile.Height, HorizontalAlignment = HorizontalAlignment.Left };",
                "Action<string> paint = id => host.Child = BuildRigPicture(tile, id);",
                "paint(scenario);",
                "Template = RigThumbTemplate,",
                "Focusable = true,",
                "IsTabStop = true,",
                "Canvas.SetLeft(root, tile.X);",
                "Canvas.SetTop(root, tile.Y);",
                "return new RigTileView(tile, root, paint);");
            // The tile's name as the driver named it, and the dot in Caution, as the sidebar's.
            // Trimmed with an ellipsis to the room beside the dot, which RigNameTrimmed's tooltip rule assumes.
            InOrder(tile, "Ui.Text(tile.Name, PanelRigMap.NameSize,", "name.TextTrimming = TextTrimming.CharacterEllipsis;",
                "name.MaxWidth = Math.Max(0, tile.Width - (warns ? PanelRigMap.WarnDot + PanelRigMap.WarnGap : 0));",
                "RigNameTrimmed(tile.Name, name.MaxWidth)");
            InOrder(tile, "Ui.Text(tile.Name, PanelRigMap.NameSize,", "nameLine.Children.Add(name);", "if (warns)", "Fill = Ui.Brush(Theme.Caution),", "body.Children.Add(nameLine);", "body.Children.Add(host);");
            // The Thumb's face is transparent rather than empty: without a background it is not hit-testable,
            // a press lands on the picture under it, and no tile can be dragged.
            InOrder(RigMethod("private static ControlTemplate RigThumbTemplate"),
                "new FrameworkElementFactory(typeof(Border))",
                "face.SetValue(Border.BackgroundProperty, Brushes.Transparent);",
                "new ControlTemplate(typeof(Thumb)) { VisualTree = face };");
            // The drag's clamp and the drop size a tile by its root, which is the whole footprint, name line
            // included: sized by the picture alone, a tile dropped at the foot deepened the room each time.
            InOrder(tile, "var root = new Grid { Width = PanelRigMap.FootprintWidth(tile), Height = PanelRigMap.FootprintHeight(tile) };", "Canvas.SetLeft(root, tile.X);");
            // A build that has been replaced writes nothing: the shell lets go of it before the new one is built.
            InOrder(canvas, "Live = true };", "OnDrop(() =>", "extent.Live = false;");

            // The arrow keys move a tile a grid step, and leave the save to the key coming up, or to focus
            // leaving first; a key that moved nothing saves nothing.
            var keyDown = Handler(tile, "thumb.KeyDown +=");
            InOrder(keyDown,
                "case Key.Left: dx = -PanelRigMap.GridStep; break;",
                "case Key.Right: dx = PanelRigMap.GridStep; break;",
                "case Key.Up: dy = -PanelRigMap.GridStep; break;",
                "case Key.Down: dy = PanelRigMap.GridStep; break;",
                "default: return;",
                "args.Handled = true;",
                "if (!RigPlace(root, Canvas.GetLeft(root) + dx, Canvas.GetTop(root) + dy, extent)) return;",
                "extent.Pending = tile;");
            Assert.DoesNotContain("RigDrop(", keyDown);
            Assert.DoesNotContain("Save(", keyDown);
            InOrder(Handler(tile, "thumb.KeyUp +="), "if (!extent.Live) return;", "if (extent.Pending != tile) return;", "extent.Pending = null;", "RigDrop(tile, views);");
            InOrder(Handler(tile, "thumb.LostKeyboardFocus +="), "if (!extent.Live) return;", "if (extent.Pending != tile) return;", "extent.Pending = null;", "RigDrop(tile, views);");
            InOrder(keyDown, "if (!extent.Live) return;", "RigPlace(");
            // A drop lands on the grid inside the canvas, where the next build draws it (bbd059f).
            var place = RigMethod("private static bool RigPlace(");
            InOrder(place, "var left = PanelRigMap.DropPosition(x, root.Width, extent.Width);", "var top = PanelRigMap.DropPosition(y, root.Height, extent.Height);");
            Assert.DoesNotContain("Snap(", place);
            // A press does not scroll the page to the tile under it.
            InOrder(tile, "var showing = false;", "root.RequestBringIntoView +=");
            InOrder(Handler(tile, "root.RequestBringIntoView +="), "if (!showing && Mouse.LeftButton == MouseButtonState.Pressed) args.Handled = true;");
            Assert.Equal(1, tile.Split("showing = true;").Length - 1);
            // A wheel button pages a face's zones, and the quick glance a face's or a pit wall's, with no save
            // and no rebuild, so the clock repaints the tile.
            InOrder(tile, "var seen = PanelRigMap.LiveState(Settings, tile);", "if (seen != null)", "OnTick(", "var now = PanelRigMap.LiveState(Settings, tile);", "if (now == seen) return;", "seen = now;", "paint(rigScenario);");
            // The warning dot is Home's list's.
            Assert.Contains("var warns = PanelRigMap.Warns(tile, issues);", tile);

            var pick = RigMethod("private void RigPick(");
            InOrder(pick, "Select(PanelPage.Rig, id);", "foreach (var view in views) view.Paint(id);", "RigDrawChips(host, views, id);");
            Assert.DoesNotContain("RebuildPage(", pick);
            Assert.DoesNotContain("Redraw(", pick);
            Assert.DoesNotContain("Save(", pick);
            // And a chip is what picks it: each group under its own title, the picked chip pressed, and one
            // picked from the keyboard focused again after its redraw.
            InOrder(RigMethod("private void RigDrawChips("),
                "var current = rigScenario;",
                // 32 between two groups and 18 between two rows of them, as the artboard's `gap: 18px 32px`:
                // the chips' own 6 counted in, and the last group's trailing gaps taken back by the panel, across
                // and down, so the last row does not leave 18 under the page (#527).
                "var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, -PanelRigMap.GroupGapX, -PanelRigMap.GroupGapY) };",
                "foreach (var group in PanelEmulation.Groups)",
                "foreach (var scenario in group.Scenarios)",
                "Ui.SwatchChip(scenario.Label, scenario.SwatchHex, id == current, () => RigPick(host, views, id))",
                // A swatch chip is named and says whether it is pressed in the kit (#523), not here.
                "chip.Margin = new Thickness(0, 0, PanelRigMap.ChipGap, PanelRigMap.ChipGap);",
                "if (id == focus) focusChip = chip;",
                "Ui.Eyebrow(group.Title)",
                "title.Margin = new Thickness(0, 0, 0, PanelRigMap.GroupTitleGap);",
                "column.Margin = new Thickness(0, 0, PanelRigMap.GroupGapX - PanelRigMap.ChipGap, PanelRigMap.GroupGapY - PanelRigMap.ChipGap);",
                "host.Child = wrap;",
                "chip.Dispatcher.BeginInvoke(new Action(() => chip.Focus()), DispatcherPriority.Input);");
            // A rebuild -- the night switch, a resize, Settings' Try -- paints the selection, not the default.
            InOrder(canvas, "var scenario = rigScenario;", "BuildRigTile(tile, extent, scenario, views)");
            Assert.Contains("return id != null && PanelEmulation.Find(id) != null ? id : PanelEmulation.Default;", RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Rig.cs")));

            // The chips' section is announced under its name: a plain Border has no automation peer, so the
            // host is the kit's GroupBorder, which reports itself as a group; the page keeps no peer of its own.
            InOrder(RigMethod("private FrameworkElement BuildRigScenarios("), "var host = new GroupBorder();", "AutomationProperties.SetName(host, PanelRigMap.ScenariosName);", "RigDrawChips(host, views, null);", "return host;");
            var rigPage = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Rig.cs"));
            Assert.DoesNotContain("RigGroup :", rigPage);
            Assert.DoesNotContain("AutomationPeer", rigPage);
        }

        /// <remarks>
        /// Rulings the page alone carries out: the model functions are tested, and these hold that the page
        /// calls them. Each was a mutation that left every other check green.
        /// </remarks>
        [Fact]
        public void The_rig_page_paints_each_tile_from_its_own_settings_and_lights_nothing_real()
        {
            // The lights dim at night (ruling 22), and the header's night switch follows a wheel's press.
            Assert.Contains("return PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);", RigMethod("private double RigLights("));
            // The page is its header, the canvas and the chips, in that order, each under the anchor search and
            // Home's fix rows route to.
            InOrder(RigMethod("private FrameworkElement BuildRigPage("),
                "DrawsLighting();",
                "var canvas = Ui.Anchor(BuildRigCanvas(views), PanelRigMap.AnchorCanvas);",
                "var chips = Ui.Anchor(BuildRigScenarios(views), PanelRigMap.AnchorScenarios);",
                // The page's section gap between the header, the canvas and the chips.
                "var gap = PanelShell.SectionGapFor(PanelPage.Rig);",
                "canvas.Margin = new Thickness(0, gap, 0, 0);",
                "chips.Margin = new Thickness(0, gap, 0, 0);",
                "stack.Children.Add(BuildRigHeader());",
                "stack.Children.Add(canvas);",
                "stack.Children.Add(chips);",
                "return stack;");

            // A strip from its shape, its switches and its centre; a matrix from its slot, critical flags only
            // included, which needs the scenario.
            var picture = RigMethod("private FrameworkElement BuildRigPicture(");
            InOrder(picture,
                "var strip = PanelRigMap.StripOptionsFor(Settings, bar);",
                "var frame = PanelEmulation.StripFrame(PanelRigMap.StripEnds(bar), PanelRigMap.StripCentre(bar), scenario, strip);",
                "Ui.Strip(frame, StripStyle.Rig, RigLights())",
                "PanelRigMap.MatrixOptionsFor(Settings, PanelRigMap.MatrixSlot(tile), scenario)",
                "Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, scenario, options), MatrixStyle.Rig, RigLights())");

            // Each kind of tile drawn by its own builder.
            InOrder(picture,
                "case RigTileKind.Round:", "return RigRound(tile, scenario);",
                "case RigTileKind.Companion:", "return RigCompanion(tile, Settings.ScreenByNamespace(tile.Key), scenario);",
                "case RigTileKind.PitWall:", "return RigPitWall(tile, Settings.ScreenByNamespace(tile.Key), scenario);",
                "default:", "return RigFace(tile, Settings.ScreenByNamespace(tile.Key), scenario);");

            // A screen's flag by its own flag format: on the band, or over the body; a face's rev strip by its
            // own Revbar, its zones on the pages they are on, and over zone A the limiter and the pop-up.
            var face = RigMethod("private FrameworkElement RigFace(");
            InOrder(face,
                "var revs = PanelRigMap.FaceRevs(scenario, Settings, screen);",
                "Background = Ui.Brush(led ?? Theme.SurfaceRaised),",
                "Height = PanelRigMap.RevRowHeight,",
                "Padding = new Thickness(PanelRigMap.RevPadX - PanelRigMap.RevGap / 2, PanelRigMap.RevPadY, PanelRigMap.RevPadX - PanelRigMap.RevGap / 2, PanelRigMap.RevPadY),",
                "PanelRigMap.FaceFlagFormat(Settings, screen)",
                "var inner = PanelRigMap.ScreenInner(tile.Width);",
                "PanelRigMap.BandPaint(PanelRigMap.FaceBandFor(scenario, format), inner)",
                "var column = PanelRigMap.FaceColumn(screen);",
                "var list = PanelRigMap.FaceZones(screen);",
                // Each zone as wide (or, stacked, as tall) as its weight, the dash's own proportions.
                "var length = new GridLength(zone.Weight, GridUnitType.Star);",
                "if (column) zones.RowDefinitions.Add(new RowDefinition { Height = length });",
                "else zones.ColumnDefinitions.Add(new ColumnDefinition { Width = length });",
                "var covered = RigCovered(zones, PanelRigMap.FaceBlockFor(scenario, format), inner);",
                "var limiter = PanelRigMap.LimiterPaint(scenario, PanelRigMap.ZoneWidth(list, \"A\", column, inner));",
                "dock.Children.Add(RigOverZoneA(covered, list, column, limiter, PanelRigMap.PopUpPaint(scenario)));",
                "return RigScreenFrame(tile, dock);");
            InOrder(RigMethod("private static UIElement RigOverZoneA("),
                "if (limiter == null && popUp == null) return body;",
                "if (zones[i].Letter == \"A\") at = i;",
                "foreach (var paint in new[] { limiter, popUp })",
                "box.Height = PanelRigMap.LimiterHeight;",
                "box.VerticalAlignment = VerticalAlignment.Top;",
                "grid.Children.Add(body);",
                "grid.Children.Add(over);");
            // A flag's block over a body where the screen draws its flags full screen, and nothing otherwise.
            InOrder(RigMethod("private static UIElement RigCovered("), "if (block == null || !block.Alert) return body;", "grid.Children.Add(body);", "grid.Children.Add(RigBand(PanelRigMap.BandPaint(block, width)));");
            var wall = RigMethod("private FrameworkElement RigPitWall(");
            InOrder(wall, "PanelRigMap.PitWallFlagFormat(Settings, screen)", "var inner = PanelRigMap.ScreenInner(tile.Width);", "PanelRigMap.BandPaint(PanelRigMap.PitWallBandFor(scenario, format), inner)", "PanelRigMap.PitWallCells(screen)", "RigCovered(body, PanelRigMap.PitWallBlockFor(scenario, format), inner)");
            InOrder(RigMethod("private FrameworkElement RigCompanion("), "PanelRigMap.CompanionFlagFormat(Settings, screen)", "PanelRigMap.CompanionPaint(PanelRigMap.CompanionBand(scenario, format), PanelRigMap.CompanionIdle(screen))", "PanelRigMap.CompanionStrip(scenario, format)");
            // The round's ring by the scenario and the rig's rev ring, and the limiter's block above its gear.
            var round = RigMethod("private FrameworkElement RigRound(");
            InOrder(round,
                "var ring = PanelRigMap.RingFor(scenario, Settings);",
                "Stroke = ring.Chequer ? RigChequer : Ui.Brush(ring.Hex),",
                "StrokeThickness = ring.Thickness,",
                "var limiter = PanelRigMap.LimiterPaint(scenario, PanelRigMap.RoundLimiterWidth);",
                "banner.Width = PanelRigMap.RoundLimiterWidth;",
                "banner.Height = PanelRigMap.LimiterHeight;",
                "banner.Margin = new Thickness(0, PanelRigMap.RoundLimiterTop, 0, 0);",
                "grid.Children.Add(banner);");
            // The screens do not dim (#128): only the lights take RigLights.
            foreach (var screen in new[] { face, wall, round, RigMethod("private FrameworkElement RigCompanion("), RigMethod("private static Border RigScreenFrame(") })
            {
                Assert.DoesNotContain("RigLights()", screen);
                Assert.DoesNotContain("Opacity", screen);
            }
            // A band's words drawn tracked where the paint says so, wrapped where it does not, and a rule along
            // the top alone where the paint asks for one.
            InOrder(RigMethod("private static Border RigPainted("),
                "if (paint.Chequer) box.Background = RigChequer;",
                "else if (paint.FillHex != null) box.Background = Ui.Brush(paint.FillHex);",
                "if (paint.BorderHex != null)",
                "box.BorderThickness = paint.RuleOnTop ? new Thickness(0, paint.BorderWidth, 0, 0) : new Thickness(paint.BorderWidth);",
                "if (paint.Tracked) text = Ui.Tracked(paint.Words, textSize, FontWeights.SemiBold, paint.InkHex, tracking);",
                "block.TextWrapping = TextWrapping.Wrap;");
            Assert.Contains("return RigPainted(paint, PanelRigMap.BandTextSize, PanelRigMap.BandTracking);", RigMethod("private static Border RigBand("));

            // Real hardware (#506) is drawn greyed, a switch with no handler, and nothing on the page
            // installs or writes to a device: the page emulates.
            var header = RigMethod("private FrameworkElement BuildRigHeader(");
            InOrder(header, "var real = PanelSoon.RealHardware;", "var realSwitch = Ui.Switch(false, null);", "Ui.Soon(Ui.HStack(PanelRigMap.HeaderLabelGap, RigHeaderLabel(real.Title), Ui.SoonTag(real), realSwitch), real)");
            // The Soon's wrapper carries the name and has a peer (#523), so the page names neither the switch
            // nor a chip itself.
            var page = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Rig.cs"));
            Assert.DoesNotContain("SetName(realSwitch", page);
            Assert.DoesNotContain("SetName(chip", page);
            Assert.DoesNotContain("SetItemStatus(chip", page);
            foreach (var write in new[] { "InstallBar(", "UpdateBars(", "InstallFlagBox(" }) Assert.DoesNotContain(write, page);
            // The page calls DrawsLighting, so a wheel's lighting press rebuilds it on SimHub's interface
            // thread: its build reads nothing of SimHub's devices, profiles or disk (foundation §4).
            foreach (var read in new[] { "LedTargets", "StripInstaller", "FlagBoxInstaller", "PackageExtractor", "SafePlan", "BarCensus" }) Assert.DoesNotContain(read, page);

            // The night switch shows the setting, saves at once, and shows the change; it is named, and it has
            // its words beside it.
            InOrder(header,
                "var night = Ui.Switch(Settings.LightsNightMode, on =>",
                "Settings.LightsNightMode = on;",
                "Save();",
                "ShowLightingChange();",
                "AutomationProperties.SetName(night, PanelSettings.NightModeTitle);",
                "Ui.HStack(PanelRigMap.HeaderLabelGap, RigHeaderLabel(PanelSettings.NightModeTitle), night)");
            // Every control of the header drawn: beside the title on two columns, wrapped under it otherwise.
            InOrder(header,
                "var controls = new FrameworkElement[] { nightGroup, hardware, reset };",
                "if (TwoColumns)",
                "Ui.HStack(PanelRigMap.HeaderGap, controls)",
                "return Ui.Row(title, right);",
                // The stacked header's gaps between and under its controls, the last line's taken back, so the
                // canvas stands its section gap under it as it does under the two-column header.
                "Margin = new Thickness(0, PanelRigMap.HeaderStackGap, -PanelRigMap.HeaderGap, -PanelRigMap.HeaderLabelGap)",
                "foreach (var control in controls)",
                "control.Margin = new Thickness(0, 0, PanelRigMap.HeaderGap, PanelRigMap.HeaderLabelGap);",
                "wrap.Children.Add(control);",
                "return Ui.VStack(0, title, wrap);");

            // The page's own words: its title and New tag, the canvas's hint, and the empty rig.
            InOrder(header, "var title = PageTitleRow(PanelRigMap.Title, Ui.NewTag());");
            var canvas = RigMethod("private FrameworkElement BuildRigCanvas(");
            InOrder(canvas, "Ui.Prose(PanelRigMap.Empty,", "Ui.Text(PanelRigMap.CanvasHint,");
        }

        /// <remarks>
        /// What each builder makes is put on the screen, at the constants The_rig_page_is_drawn_at_the_artboards_sizes
        /// holds by value (#527). Each line was a mutation that left every other check green: a tile with no
        /// Thumb could not be dragged, a face that never added its band or its zones drew none, and a name line
        /// sized off NameHeight broke FootprintHeight, which the drag's clamp and the drop size a tile by. The
        /// ones the model's arithmetic assumes come first in each list.
        /// </remarks>
        [Fact]
        public void The_rig_page_adds_what_it_builds_and_draws_it_at_its_constants()
        {
            // A tile: the name line at NameHeight and NameGap and the dot at WarnDot and WarnGap, which
            // FootprintHeight and the name's MaxWidth assume; the picture and the Thumb on the root, the Thumb a
            // focusable tab stop wearing the kit's ring, which RigRingBounds' outset is measured for.
            InOrder(RigMethod("private RigTileView BuildRigTile("),
                "var name = Ui.Text(tile.Name, PanelRigMap.NameSize, FontWeights.Medium, Theme.TextSecondary);",
                "Height = PanelRigMap.NameHeight,",
                "Margin = new Thickness(0, 0, 0, PanelRigMap.NameGap),",
                "nameLine.Children.Add(name);",
                "if (warns)",
                "nameLine.Children.Add(new Ellipse",
                "Width = PanelRigMap.WarnDot,",
                "Height = PanelRigMap.WarnDot,",
                "Fill = Ui.Brush(Theme.Caution),",
                "Margin = new Thickness(PanelRigMap.WarnGap, 0, 0, 0),",
                "body.Children.Add(nameLine);",
                "body.Children.Add(host);",
                "Template = RigThumbTemplate,",
                "Cursor = Cursors.SizeAll,",
                "Focusable = true,",
                "IsTabStop = true,",
                "FocusVisualStyle = Ui.FocusRing(),",
                "var root = new Grid { Width = PanelRigMap.FootprintWidth(tile), Height = PanelRigMap.FootprintHeight(tile) };",
                "root.Children.Add(body);",
                "root.Children.Add(thumb);",
                "Canvas.SetLeft(root, tile.X);",
                "Canvas.SetTop(root, tile.Y);");

            // A drop or a key lands on the grid and says whether it moved the tile: a key held against an edge
            // moves nothing and so saves nothing, where a "moved" that is always true kept the default layout as
            // an arrangement.
            InOrder(RigMethod("private static bool RigPlace("),
                "var left = PanelRigMap.DropPosition(x, root.Width, extent.Width);",
                "var top = PanelRigMap.DropPosition(y, root.Height, extent.Height);",
                "var moved = left != Canvas.GetLeft(root) || top != Canvas.GetTop(root);",
                "Canvas.SetLeft(root, left);",
                "Canvas.SetTop(root, top);",
                "return moved;");

            // A screen's frame at ScreenPadding, which ScreenInner and the band-fit check assume.
            InOrder(RigMethod("private static Border RigScreenFrame("),
                "Width = tile.Width,",
                "Height = tile.Height,",
                "Padding = new Thickness(PanelRigMap.ScreenPadding),",
                "Background = Ui.Brush(Theme.SurfaceInset),",
                "BorderBrush = Ui.Brush(Theme.Border),",
                "BorderThickness = new Thickness(PanelMetrics.BorderWeight),",
                "CornerRadius = new CornerRadius(PanelRigMap.ScreenRadius),",
                "Child = child,");

            // A face: its band at BandHeight under the zones, its revs, band and zones each added.
            InOrder(RigMethod("private FrameworkElement RigFace("),
                "var segments = new UniformGrid { Rows = 1, Columns = revs.Length };",
                "segments.Children.Add(new Border",
                "Margin = new Thickness(PanelRigMap.RevGap / 2, 0, PanelRigMap.RevGap / 2, 0),",
                "CornerRadius = new CornerRadius(PanelRigMap.RevRadius),",
                "Background = Ui.Brush(led ?? Theme.SurfaceRaised),",
                "Height = PanelRigMap.RevRowHeight,",
                "Background = Ui.Brush(Theme.SurfaceBase),",
                "Margin = new Thickness(0, 0, 0, PanelRigMap.ScreenGap),",
                "Child = segments,",
                "DockPanel.SetDock(revRow, Dock.Top);",
                "dock.Children.Add(revRow);",
                "band.Height = PanelRigMap.BandHeight(tile.Height);",
                "band.Margin = new Thickness(0, PanelRigMap.ScreenGap, 0, 0);",
                "DockPanel.SetDock(band, Dock.Bottom);",
                "dock.Children.Add(band);",
                "text = Ui.Text(zone.Text, PanelRigMap.FaceGearSize(tile.Height, column), FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);",
                "text = Ui.Text(zone.Text, PanelRigMap.ZoneTextSize, FontWeights.Normal, Theme.TextSecondary);",
                "Background = Ui.Brush(Theme.SurfaceZone),",
                "Child = text,",
                "zones.Children.Add(cell);",
                "dock.Children.Add(RigOverZoneA(",
                "return RigScreenFrame(tile, dock);");
            // The limiter's banner and the pop-up are added over zone A, over the body.
            InOrder(RigMethod("private static UIElement RigOverZoneA("),
                "var box = RigBand(paint);",
                "box.Margin = margin;",
                "over.Children.Add(box);",
                "var grid = new Grid();",
                "grid.Children.Add(body);",
                "grid.Children.Add(over);",
                "return grid;");

            // A pit wall: its band at PitWallBandHeight, which bodyHeight takes off, and each panel added.
            InOrder(RigMethod("private FrameworkElement RigPitWall("),
                "band.Height = PanelRigMap.PitWallBandHeight;",
                "band.Margin = new Thickness(0, 0, 0, PanelRigMap.ScreenGap);",
                "DockPanel.SetDock(band, Dock.Top);",
                "dock.Children.Add(band);",
                "var bodyHeight = tile.Height - 2 * (PanelRigMap.ScreenPadding + PanelMetrics.BorderWeight) - PanelRigMap.PitWallBandHeight - PanelRigMap.ScreenGap;",
                "Padding = new Thickness(PanelRigMap.PitWallCellPadding),",
                "Background = Ui.Brush(Theme.SurfaceZone),",
                "body.Children.Add(box);",
                "dock.Children.Add(RigCovered(body,",
                "return RigScreenFrame(tile, dock);");

            // A phone: its corners, its words in, and the flag's strip at its foot when the format asks.
            InOrder(RigMethod("private FrameworkElement RigCompanion("),
                "var phone = RigPainted(paint, PanelRigMap.CompanionTextSize, PanelRigMap.CompanionTracking);",
                "phone.Width = tile.Width;",
                "phone.Height = tile.Height;",
                "phone.CornerRadius = new CornerRadius(PanelRigMap.CompanionRadius);",
                "if (words != null) words.Margin = new Thickness(PanelRigMap.ScreenPadding);",
                "bar.Height = PanelRigMap.CompanionStripHeight;",
                "bar.Margin = new Thickness(PanelRigMap.ScreenPadding, 0, PanelRigMap.ScreenPadding, PanelRigMap.ScreenPadding);",
                "DockPanel.SetDock(bar, Dock.Bottom);",
                "dock.Children.Add(bar);",
                "phone.Child = null;",
                "if (words != null) dock.Children.Add(words);",
                "phone.Child = dock;",
                "return phone;");

            // A round face: its ground, and the gear added inside the ring.
            InOrder(RigMethod("private FrameworkElement RigRound("),
                "Fill = Ui.Brush(Theme.SurfaceInset),",
                "var gear = Ui.Text(PanelEmulation.Gear, PanelRigMap.RoundGearSize, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);",
                "grid.Children.Add(gear);",
                "return grid;");

            // The chequer: the base ground and two squares of the flag's white, tiled.
            InOrder(RigMethod("private static Brush RigChequerBrush("),
                "new GeometryDrawing(Ui.Brush(Theme.SurfaceBase), null, new RectangleGeometry(new Rect(0, 0, 2 * square, 2 * square)))",
                "new GeometryDrawing(Ui.Brush(Theme.FlagChequer), null, new RectangleGeometry(new Rect(square, 0, square, square)))",
                "new GeometryDrawing(Ui.Brush(Theme.FlagChequer), null, new RectangleGeometry(new Rect(0, square, square, square)))",
                "TileMode = TileMode.Tile,",
                "brush.Freeze();");

            // The chips are added to their group, the group to the wrap, and the wrap to the host.
            InOrder(RigMethod("private void RigDrawChips("),
                "chips.Children.Add(chip);",
                "var title = Ui.Eyebrow(group.Title);",
                "var column = Ui.VStack(0, title, chips);",
                "wrap.Children.Add(column);",
                "host.Child = wrap;");

            // The header's labels, the canvas's outline, its empty rig and its hint, in the artboard's sizes
            // and inks.
            InOrder(RigMethod("private static TextBlock RigHeaderLabel("),
                "var label = Ui.Text(text, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);",
                "label.VerticalAlignment = VerticalAlignment.Center;");
            InOrder(RigMethod("private FrameworkElement BuildRigCanvas("),
                "var screensPress = Ui.Button(PanelAddScreen.SectionTitle, PanelButtonKind.Outline, PanelButtonSize.Small);",
                "var empty = Ui.HStack(PanelRigMap.EmptyGap, Ui.Prose(PanelRigMap.Empty, Theme.SizeBody), screensPress);",
                "Canvas.SetLeft(empty, PanelRigMap.LayoutMargin);",
                "Canvas.SetTop(empty, PanelRigMap.LayoutMargin);",
                "hint = Ui.Text(PanelRigMap.CanvasHint, Theme.SizeLabel, FontWeights.Normal, Theme.TextLabel);",
                "Background = Ui.Brush(Theme.SurfaceInset),",
                "BorderBrush = Ui.Brush(Theme.Rule),",
                "BorderThickness = new Thickness(PanelMetrics.BorderWeight),",
                "CornerRadius = new CornerRadius(Theme.Radius),",
                "Child = frame,");
        }

        [Fact]
        public void Every_device_on_the_rig_is_a_tile_at_the_size_its_picture_is_drawn()
        {
            var tiles = PanelRigMap.Tiles(Rig());
            Assert.Equal(new[] { "MainDash", "Rim", "PitWall", "Companion", "Slots480x480", "led:LedDashBrow", "led:LedWheelRim", "matrix:1", "matrix:2" }, tiles.Select(t => t.Id));
            Assert.Equal(new[] { "MainDash", "Rim", "PitWall", "Companion", "Slots480x480", "LedDashBrow", "LedWheelRim", "1", "2" }, tiles.Select(t => t.Key));
            Assert.Equal(new[] { "Main dash", "Rim", "Pit wall", "Phone", "Round", "Dash brow", "Wheel rim", "Left pillar", "Flag box" }, tiles.Select(t => t.Name));
            Assert.Equal(new[] { RigTileKind.Face, RigTileKind.Face, RigTileKind.PitWall, RigTileKind.Companion, RigTileKind.Round, RigTileKind.Strip, RigTileKind.Strip, RigTileKind.Matrix, RigTileKind.Matrix }, tiles.Select(t => t.Kind));
            // A 1280 x 480 face at 300 across, as the artboard's rim: 300 x 112.
            Assert.Equal(new[] { 300.0, 112 }, new[] { tiles[0].Width, tiles[0].Height });
            // The 1920 x 1080 pit wall fills the 240 x 135 box; the 480 x 850 phone is 135 high.
            Assert.Equal(new[] { 240.0, 135 }, new[] { tiles[2].Width, tiles[2].Height });
            Assert.Equal(new[] { 76.0, 135 }, new[] { tiles[3].Width, tiles[3].Height });
            Assert.Equal(new[] { 110.0, 110 }, new[] { tiles[4].Width, tiles[4].Height });
            // A bare run of 15: fifteen 14 px LEDs 3 apart, 8 in and 6 down, and the frame.
            Assert.Equal(15 * 14 + 14 * 3 + 2 * 8 + 2, tiles[5].Width);
            Assert.Equal(14 + 2 * 6 + 2, tiles[5].Height);
            // 3 / 9 / 3: three groups, 8 between them.
            Assert.Equal(15 * 14 + 12 * 3 + 2 * 8 + 2 * 8 + 2, tiles[6].Width);
            // An 8 x 8 of 10 px cells 2 apart, 6 in, and the frame.
            Assert.Equal(8 * 10 + 7 * 2 + 2 * 6 + 2, tiles[7].Width);
            Assert.Equal(tiles[7].Width, tiles[7].Height);
            Assert.Equal(PanelRigMap.MatrixTileSide(), tiles[8].Width);
            Assert.Equal(1, PanelRigMap.MatrixSlot(tiles[7]));
            Assert.Equal(0, PanelRigMap.MatrixSlot(tiles[0]));
            Assert.Empty(PanelRigMap.Tiles(null));
        }

        [Fact]
        public void A_face_takes_its_screens_own_aspect()
        {
            double w, h;
            PanelRigMap.FaceTileSize(1920, 480, out w, out h);
            Assert.Equal(new[] { 300.0, 75 }, new[] { w, h });
            PanelRigMap.FaceTileSize(800, 480, out w, out h);
            Assert.Equal(new[] { 300.0, 180 }, new[] { w, h });
            // Taller than 180 at 300 across: drawn 180 down and narrower instead.
            PanelRigMap.FaceTileSize(600, 686, out w, out h);
            Assert.Equal(new[] { 157.0, 180 }, new[] { w, h });
            // A screen of no known size is drawn as the artboard's 1280 x 480.
            PanelRigMap.FaceTileSize(0, 0, out w, out h);
            Assert.Equal(new[] { 300.0, 112 }, new[] { w, h });
        }

        [Fact]
        public void A_pit_wall_and_a_phone_take_their_screens_own_aspect()
        {
            var settings = Rig();
            settings.Rig = new List<ScreenInstance>
            {
                Screen(Contract.KindCompanion, "Companion", "Phone", 850, 480),
                Screen(Contract.KindCompanion, "CompanionPortrait", "Phone portrait", 480, 850),
                Screen(Contract.KindPitWall, "PitWall", "Pit wall", 1920, 1080),
                Screen(Contract.KindPitWall, "PitWallPortrait", "Pit wall portrait", 1080, 1920),
                Screen(Contract.KindCompanion, "Unknown", "Phone of no size", 0, 0),
                Screen(Contract.KindPitWall, "UnknownWall", "Pit wall of no size", 0, 0),
            };
            var tiles = PanelRigMap.Tiles(settings).Take(6).ToList();
            Assert.Equal(new[] { RigTileKind.Companion, RigTileKind.Companion, RigTileKind.PitWall, RigTileKind.PitWall, RigTileKind.Companion, RigTileKind.PitWall }, tiles.Select(t => t.Kind));
            // The default phone is landscape, and drawn so; 850 x 480 is a hair taller than 16:9.
            Assert.Equal(new[] { 239.0, 135 }, new[] { tiles[0].Width, tiles[0].Height });
            Assert.True(tiles[0].Width > tiles[0].Height);
            Assert.Equal(new[] { 76.0, 135 }, new[] { tiles[1].Width, tiles[1].Height });
            Assert.Equal(new[] { 240.0, 135 }, new[] { tiles[2].Width, tiles[2].Height });
            Assert.Equal(new[] { 75.0, 135 }, new[] { tiles[3].Width, tiles[3].Height });
            // A screen of no known size is the artboard's.
            Assert.Equal(new[] { 76.0, 135 }, new[] { tiles[4].Width, tiles[4].Height });
            Assert.Equal(new[] { 240.0, 135 }, new[] { tiles[5].Width, tiles[5].Height });
        }

        [Fact]
        public void The_default_layout_puts_strips_on_top_faces_in_the_centre_and_the_rest_at_the_flanks()
        {
            var tiles = PanelRigMap.Tiles(Rig());
            // The artboard's canvas: 1200 less the 216 sidebar, the 44 either side and the frame.
            const double width = 894;
            var placed = PanelRigMap.DefaultLayout(tiles, width);
            Assert.Equal(tiles.Select(t => t.Id), placed.Select(t => t.Id));
            var at = placed.ToDictionary(t => t.Id);
            var main = at["MainDash"];
            var rim = at["Rim"];
            var brow = at["led:LedDashBrow"];
            var wheel = at["led:LedWheelRim"];
            var pillar = at["matrix:1"];
            var box = at["matrix:2"];
            var round = at["Slots480x480"];
            var pit = at["PitWall"];
            var phone = at["Companion"];

            // The strips share the top row, centred across the canvas.
            Assert.Equal(24, brow.Y);
            Assert.Equal(24, wheel.Y);
            Assert.Equal(width - (wheel.X + wheel.Width), brow.X, 0);
            var under = 24 + PanelRigMap.FootprintHeight(brow) + 28;
            // The faces under them, one over the other, centred between the flanks.
            Assert.Equal(under, main.Y);
            Assert.Equal(main.Y + PanelRigMap.FootprintHeight(main) + 28, rim.Y);
            Assert.Equal(main.X, rim.X);
            // The first matrix on the left flank, the second on the right, each 28 from the faces, as the
            // artboard stands its pillar and its flag box beside the main dash; and the three centred in the
            // canvas as one group, the pit wall being the right flank's widest.
            Assert.Equal(main.X - 28 - pillar.Width, pillar.X);
            Assert.Equal(main.X + main.Width + 28, box.X);
            Assert.Equal(95, pillar.X);
            Assert.Equal(pillar.X, width - (pit.X + pit.Width));
            Assert.Equal(under, pillar.Y);
            Assert.Equal(under, box.Y);
            // Down the right: the matrix, the round, then the pit wall, closed up from 28 to fit the 580.
            Assert.Equal(box.X, round.X);
            Assert.Equal(box.X, pit.X);
            var closed = Math.Floor((580 - 24 - under - PanelRigMap.FootprintHeight(box) - PanelRigMap.FootprintHeight(round) - PanelRigMap.FootprintHeight(pit)) / 2);
            Assert.Equal(17, closed);
            Assert.Equal(box.Y + PanelRigMap.FootprintHeight(box) + closed, round.Y);
            Assert.Equal(round.Y + PanelRigMap.FootprintHeight(round) + closed, pit.Y);
            // Down the left: the phone under the matrix, since the foot of the faces is higher than that.
            Assert.Equal(pillar.X, phone.X);
            Assert.Equal(pillar.Y + PanelRigMap.FootprintHeight(pillar) + 28, phone.Y);
            // Nothing past the right margin or the foot.
            Assert.All(placed, t => Assert.True(t.X + t.Width <= width - 24 && t.Y + PanelRigMap.FootprintHeight(t) <= 580 - 24, t.Id));
            // The same rig lays out the same way every time.
            Assert.Equal(placed.Select(t => t.X + "," + t.Y), PanelRigMap.DefaultLayout(tiles, width).Select(t => t.X + "," + t.Y));

            // Given the room, the two faces share a row, and the phone drops to the foot of the faces.
            var wide = PanelRigMap.DefaultLayout(tiles, 1100).ToDictionary(t => t.Id);
            Assert.Equal(wide["MainDash"].Y, wide["Rim"].Y);
            Assert.Equal(wide["MainDash"].X + 300 + 28, wide["Rim"].X);
            Assert.Equal(wide["matrix:1"].X, wide["Companion"].X);
            Assert.Equal(wide["MainDash"].X - 28 - 108, wide["matrix:1"].X);
            Assert.Equal(wide["matrix:1"].Y + PanelRigMap.FootprintHeight(wide["matrix:1"]) + 28, wide["Companion"].Y);
        }

        [Fact]
        public void A_wide_window_centres_the_rig_rather_than_pulling_its_flanks_apart()
        {
            var tiles = PanelRigMap.Tiles(Rig());
            foreach (var width in new[] { 894.0, 1377, 2017, 3600 })
            {
                var at = PanelRigMap.DefaultLayout(tiles, width).ToDictionary(t => t.Id);
                // The left pillar stands 28 from the faces however wide the window, as the right flank does.
                Assert.Equal(28, at["MainDash"].X - (at["matrix:1"].X + at["matrix:1"].Width));
                var facesRight = Math.Max(at["MainDash"].X, at["Rim"].X) + 300;
                Assert.Equal(28, at["matrix:2"].X - facesRight);
                // And the group is as far from the left edge as from the right, to the pixel the floor takes.
                var left = at["matrix:1"].X;
                var right = width - (at["PitWall"].X + at["PitWall"].Width);
                Assert.InRange(right - left, 0, 1);
            }
        }

        [Fact]
        public void What_sits_low_on_a_flank_is_level_with_the_foot_of_the_faces()
        {
            var tiles = new[] { Tile(RigTileKind.Face, "a", 300, 112), Tile(RigTileKind.Face, "b", 300, 112), Tile(RigTileKind.Companion, "phone", 76, 135), Tile(RigTileKind.PitWall, "wall", 240, 135) };
            var placed = PanelRigMap.DefaultLayout(tiles, 894).ToDictionary(t => t.Id);
            var foot = placed["b"].Y + PanelRigMap.FootprintHeight(placed["b"]);
            Assert.Equal(foot, placed["phone"].Y + PanelRigMap.FootprintHeight(placed["phone"]));
            Assert.Equal(foot, placed["wall"].Y + PanelRigMap.FootprintHeight(placed["wall"]));
            Assert.Equal(placed["a"].X - 28 - 76, placed["phone"].X);
            Assert.Equal(placed["a"].X + 300 + 28, placed["wall"].X);
        }

        [Fact]
        public void Where_there_is_no_room_between_the_flanks_the_tiles_fall_back_to_rows_by_kind()
        {
            var tiles = PanelRigMap.Tiles(Rig());
            // 600 is too narrow for a 300 face between a 108 matrix and a 240 pit wall.
            double height;
            var placed = PanelRigMap.DefaultLayout(tiles, 600, out height).ToDictionary(t => t.Id);
            var brow = placed["led:LedDashBrow"];
            var wheel = placed["led:LedWheelRim"];
            Assert.Equal(24, brow.X);
            Assert.Equal(24, brow.Y);
            // 24 + 270 + 28 + 280 passes 576, so the wheel rim wraps: each row one gap under the one before.
            Assert.Equal(24, wheel.X);
            Assert.Equal(brow.Y + PanelRigMap.FootprintHeight(brow) + 28, wheel.Y);
            // Then the screens, starting a row of their own, wrapped where the next would pass the canvas.
            Assert.Equal(24, placed["MainDash"].X);
            Assert.Equal(wheel.Y + PanelRigMap.FootprintHeight(wheel) + 28, placed["MainDash"].Y);
            Assert.Equal(placed["MainDash"].Y + PanelRigMap.FootprintHeight(placed["MainDash"]) + 28, placed["Rim"].Y);
            Assert.Equal(placed["Rim"].Y, placed["Slots480x480"].Y);
            Assert.Equal(24 + 300 + 28, placed["Slots480x480"].X);
            // The matrices a row of their own under the tallest screen of the last row.
            var wall = placed["PitWall"];
            Assert.Equal(wall.Y, placed["Companion"].Y);
            Assert.Equal(24, placed["matrix:1"].X);
            Assert.Equal(wall.Y + PanelRigMap.FootprintHeight(wall) + 28, placed["matrix:1"].Y);
            Assert.All(placed.Values, t => Assert.True(t.X + t.Width <= 600 - 24, t.Id));
            // The canvas grows to hold them, the last row clear of the hint.
            Assert.Equal(placed["matrix:1"].Y + PanelRigMap.FootprintHeight(placed["matrix:1"]) + PanelRigMap.HintClear, height);
            Assert.True(height > PanelRigMap.CanvasHeight);
            // And the frame is drawn that tall, not the 580 that would clip the matrices off its foot.
            Assert.Equal(height, PanelRigMap.Plan(Rig(), 600).DrawnHeight);
        }

        [Fact]
        public void A_rig_too_tall_for_its_flanks_falls_back_to_rows()
        {
            // Two matrices, a round and a pit wall down the right need 597 even closed up to 8, past the 556.
            var tiles = new[]
            {
                Tile(RigTileKind.Matrix, "m1", 108, 108), Tile(RigTileKind.Matrix, "m2", 108, 108),
                Tile(RigTileKind.Matrix, "m3", 108, 108), Tile(RigTileKind.Matrix, "m4", 108, 108),
                Tile(RigTileKind.Round, "round", 110, 110), Tile(RigTileKind.PitWall, "wall", 240, 135),
            };
            double height;
            var placed = PanelRigMap.DefaultLayout(tiles, 894, out height).ToDictionary(t => t.Id);
            Assert.Equal(new double[] { 24, 24 }, new[] { placed["round"].X, placed["round"].Y });
            Assert.Equal(new double[] { 24 + 110 + 28, 24 }, new[] { placed["wall"].X, placed["wall"].Y });
            var row = 24 + PanelRigMap.FootprintHeight(placed["wall"]) + 28;
            Assert.Equal(new double[] { 24, 160, 296, 432 }, new[] { "m1", "m2", "m3", "m4" }.Select(id => placed[id].X));
            Assert.All(new[] { "m1", "m2", "m3", "m4" }, id => Assert.Equal(row, placed[id].Y));
            // Short enough for the 580 once in rows.
            Assert.Equal(PanelRigMap.CanvasHeight, height);
        }

        [Fact]
        public void A_matrix_stands_on_the_flank_it_is_mounted_on()
        {
            // The #503 VM rig: the flag box (both sides) in slot 1, the left pillar in slot 2. Slot order put
            // the pillar on the right, the reverse of the artboard's home layout.
            var settings = Rig();
            settings.FlagBoxMatrixName[0] = "Flag box";
            settings.FlagBoxMatrixName[1] = "Left pillar";
            settings.FlagBoxSide[1] = "left";
            var tiles = PanelRigMap.Tiles(settings);
            Assert.Equal(new[] { "both", "left" }, tiles.Where(t => t.Kind == RigTileKind.Matrix).Select(t => t.Side));
            Assert.All(tiles.Where(t => t.Kind != RigTileKind.Matrix), t => Assert.Null(t.Side));
            Assert.Equal("left", tiles.Single(t => t.Id == "matrix:2").At(5, 5).Side);
            var at = PanelRigMap.DefaultLayout(tiles, 894).ToDictionary(t => t.Id);
            Assert.True(at["matrix:2"].X < at["MainDash"].X, "the left pillar on the left");
            Assert.True(at["matrix:1"].X > at["MainDash"].X, "the flag box on the right");
            Assert.Equal(at["MainDash"].X - 28 - at["matrix:2"].Width, at["matrix:2"].X);
            // Reset layout restores exactly that.
            PanelRigMap.SavePlaces(settings, PanelRigMap.Plan(settings, 894).Tiles);
            PanelRigMap.ClearLayout(settings);
            Assert.True(ById(PanelRigMap.Plan(settings, 894))["matrix:2"].X < ById(PanelRigMap.Plan(settings, 894))["MainDash"].X);

            // A right-mounted panel on the right whatever its slot; those on both sides make up the flanks.
            var three = new[]
            {
                Tile(RigTileKind.Face, "face", 300, 112),
                new RigTile(RigTileKind.Matrix, "m1", "m1", 0, 0, 108, 108, "right"),
                new RigTile(RigTileKind.Matrix, "m2", "m2", 0, 0, 108, 108, "both"),
                new RigTile(RigTileKind.Matrix, "m3", "m3", 0, 0, 108, 108, "right"),
            };
            var laid = PanelRigMap.DefaultLayout(three, 894).ToDictionary(t => t.Id);
            Assert.True(laid["m1"].X > laid["face"].X);
            Assert.True(laid["m3"].X > laid["face"].X);
            Assert.True(laid["m2"].X < laid["face"].X);
            // A flank keeps the slot order down it.
            Assert.True(laid["m1"].Y < laid["m3"].Y);
            // Every panel on the left: all of them on the left flank.
            var lefts = new[] { Tile(RigTileKind.Face, "face", 300, 112), new RigTile(RigTileKind.Matrix, "a", "a", 0, 0, 108, 108, "left"), new RigTile(RigTileKind.Matrix, "b", "b", 0, 0, 108, 108, "left") };
            var left = PanelRigMap.DefaultLayout(lefts, 894).ToDictionary(t => t.Id);
            Assert.Equal(left["a"].X, left["b"].X);
            Assert.True(left["a"].X < left["face"].X);
        }

        [Fact]
        public void A_lone_matrix_takes_the_left_flank()
        {
            var placed = PanelRigMap.DefaultLayout(new[] { Tile(RigTileKind.Face, "face", 300, 112), Tile(RigTileKind.Matrix, "m", 108, 108) }, 894).ToDictionary(t => t.Id);
            Assert.Equal(placed["face"].X - 28 - 108, placed["m"].X);
            Assert.Equal(placed["face"].Y, placed["m"].Y);
        }

        [Fact]
        public void At_every_width_the_default_layout_stays_inside_the_canvas_and_overlaps_nothing()
        {
            var settings = Rig();
            for (var width = 400; width <= 1400; width += 3)
            {
                var plan = PanelRigMap.Plan(settings, width);
                Assert.True(plan.Height >= PanelRigMap.CanvasHeight);
                var tiles = plan.Tiles;
                foreach (var tile in tiles)
                {
                    Assert.True(tile.X >= 0 && tile.X + PanelRigMap.FootprintWidth(tile) <= width, width + " " + tile.Id);
                    Assert.True(tile.Y >= 0 && tile.Y + PanelRigMap.FootprintHeight(tile) <= plan.Height, width + " " + tile.Id);
                }
                for (var i = 0; i < tiles.Count; i++)
                {
                    for (var j = i + 1; j < tiles.Count; j++)
                    {
                        var a = tiles[i];
                        var b = tiles[j];
                        var overlap = a.X < b.X + PanelRigMap.FootprintWidth(b) && b.X < a.X + PanelRigMap.FootprintWidth(a)
                            && a.Y < b.Y + PanelRigMap.FootprintHeight(b) && b.Y < a.Y + PanelRigMap.FootprintHeight(a);
                        Assert.False(overlap, width + ": " + a.Id + " over " + b.Id);
                    }
                }
            }
        }

        [Fact]
        public void A_row_that_would_pass_the_canvas_wraps()
        {
            var tiles = Enumerable.Range(0, 4).Select(i => Tile(RigTileKind.Face, "f" + i, 300, 80)).ToArray();
            // Two faces side by side need 24 + 300 + 28 + 300 + 24 = 676, so a 700 canvas takes two a row,
            // each row centred.
            var placed = PanelRigMap.DefaultLayout(tiles, 700);
            Assert.Equal(new double[] { 36, 364, 36, 364 }, placed.Select(t => t.X));
            Assert.Equal(placed[0].Y, placed[1].Y);
            Assert.Equal(placed[0].Y + 80 + 16 + 6 + 28, placed[2].Y);
            // A tile wider than the canvas still starts a row of its own rather than vanishing.
            Assert.Single(PanelRigMap.DefaultLayout(new[] { Tile(RigTileKind.Face, "wide", 900, 80) }, 700));
            Assert.Empty(PanelRigMap.DefaultLayout(null, 1000));
        }

        [Fact]
        public void Tiles_of_one_kind_keep_the_rigs_order()
        {
            // The fallback is the one layout that sorts, so both cases are forced into it by a face wider than
            // the 700 canvas. Twenty strips, which List.Sort's introsort put out of order above sixteen, read
            // in the rig's order along their rows.
            var strips = Enumerable.Range(0, 20).Select(i => Tile(RigTileKind.Strip, "s" + i, 10, 10)).ToList();
            var rim = Tile(RigTileKind.Face, "Rim", 900, 80);
            var placed = PanelRigMap.DefaultLayout(strips.Concat(new[] { rim }), 700);
            Assert.Equal(24, placed[20].X);
            Assert.True(placed[20].Y > placed[19].Y);
            Assert.Equal(strips.Select(t => t.Id), placed.Take(20).OrderBy(t => t.Y).ThenBy(t => t.X).Select(t => t.Id));
            // Pit wall A before pit wall B, as the rig lists them; List.Sort laid them out as the rim, B, A.
            var walls = new[] { Tile(RigTileKind.PitWall, "A"), Tile(RigTileKind.PitWall, "B"), rim };
            var laid = PanelRigMap.DefaultLayout(walls, 700);
            Assert.Equal(new double[] { 24, 24 }, new[] { laid[2].X, laid[2].Y });
            Assert.Equal(laid[0].Y, laid[1].Y);
            Assert.Equal(24 + PanelRigMap.FootprintHeight(rim) + 28, laid[0].Y);
            Assert.Equal(24, laid[0].X);
            Assert.Equal(24 + 100 + 28, laid[1].X);

            // The main layout keeps the rig's order down a flank too: pit wall A over B on the right, the
            // first phone over the second on the left, and the first round over the second.
            var face = Tile(RigTileKind.Face, "F", 300, 112);
            var flanks = PanelRigMap.DefaultLayout(new[]
            {
                Tile(RigTileKind.PitWall, "A"), Tile(RigTileKind.PitWall, "B"),
                Tile(RigTileKind.Companion, "P1", 50, 90), Tile(RigTileKind.Companion, "P2", 50, 90),
                Tile(RigTileKind.Round, "R1", 60, 60), Tile(RigTileKind.Round, "R2", 60, 60),
                face,
            }, 1100);
            var at = flanks.ToDictionary(t => t.Id);
            Assert.True(at["A"].X > at["F"].X && at["R1"].X > at["F"].X, "the right flank");
            Assert.True(at["P1"].X < at["F"].X, "the left flank");
            Assert.Equal(at["A"].X, at["B"].X);
            Assert.True(at["A"].Y < at["B"].Y, "pit wall A over B");
            Assert.Equal(at["P1"].X, at["P2"].X);
            Assert.True(at["P1"].Y < at["P2"].Y, "the first phone over the second");
            Assert.Equal(at["R1"].X, at["R2"].X);
            Assert.True(at["R1"].Y < at["R2"].Y, "the first round over the second");
        }

        [Fact]
        public void A_dragged_tile_is_held_inside_all_four_edges_and_dropped_on_the_grid()
        {
            Assert.Equal(0, PanelRigMap.Clamp(-40, 100, 800));
            Assert.Equal(700, PanelRigMap.Clamp(760, 100, 800));
            Assert.Equal(250, PanelRigMap.Clamp(250, 100, 800));
            // Bigger than the canvas: at the start.
            Assert.Equal(0, PanelRigMap.Clamp(30, 900, 800));
            Assert.Equal(40, PanelRigMap.Snap(49));
            Assert.Equal(60, PanelRigMap.Snap(50));
            Assert.Equal(0, PanelRigMap.Snap(9));
            Assert.Equal(20, PanelRigMap.Snap(10));
            // A drop lands on the grid, the last step inside an edge that is not on it, and never before 0.
            Assert.Equal(40, PanelRigMap.DropPosition(49, 100, 800));
            Assert.Equal(0, PanelRigMap.DropPosition(-30, 100, 800));
            Assert.Equal(700, PanelRigMap.DropPosition(790, 100, 800));
            Assert.Equal(780, PanelRigMap.DropPosition(783, 300, 1083));
            Assert.Equal(440, PanelRigMap.DropPosition(446, 134, 580));
            Assert.Equal(0, PanelRigMap.DropPosition(10, 900, 800));
        }

        /// <summary>A drop as the page makes one: the plan at a width, one tile moved to where DropPosition
        /// lands it in the plan's room, and every tile kept where it is drawn (SettingsControl.Rig's RigDrop).</summary>
        private static RigPlan Drop(OpenDashSettings settings, double width, string id, double x, double y)
        {
            var plan = PanelRigMap.Plan(settings, width);
            var tiles = plan.Tiles.Select(t => t.Id != id ? t : t.At(
                PanelRigMap.DropPosition(x, PanelRigMap.FootprintWidth(t), plan.Width),
                PanelRigMap.DropPosition(y, PanelRigMap.FootprintHeight(t), plan.Height))).ToList();
            PanelRigMap.SavePlaces(settings, tiles);
            return new RigPlan(tiles, plan.Width, plan.Height, plan.Scale);
        }

        private static bool Overlap(RigTile a, RigTile b)
        {
            return a.X < b.X + PanelRigMap.FootprintWidth(b) && b.X < a.X + PanelRigMap.FootprintWidth(a)
                && a.Y < b.Y + PanelRigMap.FootprintHeight(b) && b.Y < a.Y + PanelRigMap.FootprintHeight(a);
        }

        /// <summary>Every pair of tiles that overlap, by their ids.</summary>
        private static ISet<string> Overlaps(RigPlan plan)
        {
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            var tiles = plan.Tiles.OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
            for (var i = 0; i < tiles.Count; i++)
            {
                for (var j = i + 1; j < tiles.Count; j++)
                {
                    if (Overlap(tiles[i], tiles[j])) pairs.Add(tiles[i].Id + " over " + tiles[j].Id);
                }
            }
            return pairs;
        }

        /// <summary>Every tile inside the plan's room, and that room drawn across the canvas's width at the
        /// plan's scale, never shrunk past <see cref="PanelRigMap.MinScale"/> -- wider than the canvas, and
        /// scrolled across, only there -- in a frame at least the 580 high.</summary>
        private static void Inside(RigPlan plan, double width, string label)
        {
            // The room in the tiles' own pixels is never under the canvas's height, whatever it is drawn at.
            Assert.True(plan.Height >= PanelRigMap.CanvasHeight, label);
            // The frame is the 580, or the room as drawn where that is taller: a frame held to the 580 under
            // a taller room clipped its lower tiles out of reach.
            Assert.Equal(Math.Max(PanelRigMap.CanvasHeight, plan.Height * plan.Scale), plan.DrawnHeight, 9);
            Assert.InRange(plan.Scale, PanelRigMap.MinScale, 1);
            if (plan.Scale > PanelRigMap.MinScale + 1e-9)
            {
                Assert.Equal(width, plan.DrawnWidth, 6);
                Assert.False(plan.Scrolls(width), label);
            }
            else Assert.True(plan.DrawnWidth >= width - 1e-6, label);
            foreach (var tile in plan.Tiles)
            {
                Assert.True(tile.X >= 0 && tile.X + PanelRigMap.FootprintWidth(tile) <= plan.Width + 1e-9, label + " " + tile.Id);
                Assert.True(tile.Y >= 0 && tile.Y + PanelRigMap.FootprintHeight(tile) <= plan.Height, label + " " + tile.Id);
            }
        }

        private static IDictionary<string, RigTile> ById(RigPlan plan)
        {
            return plan.Tiles.ToDictionary(t => t.Id);
        }

        [Fact]
        public void A_tile_dropped_against_an_edge_is_drawn_again_where_it_was_dropped()
        {
            var settings = Rig();
            const double width = 1083;
            var plan = PanelRigMap.Plan(settings, width);
            // Pushed past the right edge and the foot.
            var dropped = ById(Drop(settings, width, "Rim", width, plan.Height))["Rim"];
            Assert.True(dropped.X + PanelRigMap.FootprintWidth(dropped) <= width);
            Assert.True(dropped.Y + PanelRigMap.FootprintHeight(dropped) <= plan.Height);
            Assert.Equal((int)dropped.X, settings.ScreenByNamespace("Rim").LayoutX);
            Assert.Equal((int)dropped.Y, settings.ScreenByNamespace("Rim").LayoutY);
            var again = PanelRigMap.Plan(settings, width);
            Assert.Equal(new[] { dropped.X, dropped.Y }, new[] { ById(again)["Rim"].X, ById(again)["Rim"].Y });
            // Against the foot, the canvas does not grow under it: a drop there again lands in the same place.
            Assert.Equal(plan.Height, again.Height);
        }

        [Fact]
        public void A_drop_keeps_every_tile_where_it_is_drawn_and_moves_no_other()
        {
            var settings = Rig();
            var before = ById(PanelRigMap.Plan(settings, 1100));

            // The dropped tile on the grid; every other where it was drawn, to the pixel, in its own settings.
            Drop(settings, 1100, "Rim", 433, 51);
            Assert.Equal(440, settings.ScreenByNamespace("Rim").LayoutX);
            Assert.Equal(60, settings.ScreenByNamespace("Rim").LayoutY);
            Assert.Equal((int)before["led:LedWheelRim"].X, settings.LedBarByNamespace("LedWheelRim").LayoutX);
            Assert.Equal((int)before["matrix:2"].Y, settings.MatrixLayoutY[1]);
            Assert.Equal((int)before["MainDash"].X, settings.ScreenByNamespace("MainDash").LayoutX);

            var tiles = PanelRigMap.Tiles(settings).ToDictionary(t => t.Id);
            int x, y;
            Assert.True(PanelRigMap.TrySaved(settings, tiles["Rim"], out x, out y));
            Assert.Equal(new[] { 440, 60 }, new[] { x, y });
            Assert.All(tiles.Values, t => Assert.True(PanelRigMap.TrySaved(settings, t, out x, out y), t.Id));

            var after = ById(PanelRigMap.Plan(settings, 1100));
            Assert.Equal(440, after["Rim"].X);
            Assert.Equal(60, after["Rim"].Y);
            foreach (var id in before.Keys.Where(id => id != "Rim"))
            {
                Assert.Equal(before[id].X, after[id].X);
                Assert.Equal(before[id].Y, after[id].Y);
            }

            // A place is kept to the pixel and never negative.
            PanelRigMap.SavePlaces(settings, new[] { tiles["matrix:2"].At(-30, 70.6) });
            Assert.Equal(0, settings.MatrixLayoutX[1]);
            Assert.Equal(71, settings.MatrixLayoutY[1]);
            PanelRigMap.SavePlaces(null, new[] { tiles["Rim"] });
            PanelRigMap.SavePlaces(settings, null);
        }

        /// <remarks>
        /// A tile a driver had placed kept absolute pixels while the default re-centred every other for each
        /// width, so an arrangement came apart whenever the sidebar changed to the rail or SimHub was
        /// resized. Arranged at every width, planned at every other: never outside the canvas, and never an
        /// overlap the arrangement did not have. Where it is wider than the canvas it is shrunk to it rather
        /// than set aside for the default, which the next drop kept over it.
        /// </remarks>
        [Fact]
        public void An_arranged_rig_keeps_its_shape_at_every_width_and_shrinks_where_it_is_wider()
        {
            foreach (var arrangedAt in new[] { 542.0, 661, 894, 1100, 1597, 3534 })
            {
                var settings = Rig();
                var arranged = PanelRigMap.Plan(settings, arrangedAt);
                // Every tile dropped in place.
                PanelRigMap.SavePlaces(settings, arranged.Tiles);
                var had = Overlaps(arranged);
                var kept = ById(arranged);
                var left = arranged.Tiles.Min(t => t.X);
                var right = arranged.Tiles.Max(t => t.X + PanelRigMap.FootprintWidth(t));
                var span = right - left;
                for (var width = 400; width <= 1700; width += 7)
                {
                    var label = arrangedAt + " at " + width;
                    var plan = PanelRigMap.Plan(settings, width);
                    Inside(plan, width, label);
                    // The arrangement as one: every tile moved by the same, and none but left.
                    Assert.Subset(had, Overlaps(plan));
                    var dx = ById(plan)["MainDash"].X - kept["MainDash"].X;
                    Assert.True(dx <= 0, label);
                    Assert.All(plan.Tiles, t => Assert.Equal(new[] { kept[t.Id].X + dx, kept[t.Id].Y }, new[] { t.X, t.Y }));
                    if (right <= width)
                    {
                        // Where it was kept.
                        Assert.Equal(0, dx);
                        Assert.Equal(1, plan.Scale);
                    }
                    else if (span <= width)
                    {
                        // Past the right edge: centred as one, at its own size.
                        Assert.Equal(1, plan.Scale);
                        var drawnLeft = plan.Tiles.Min(t => t.X);
                        Assert.InRange(width - (drawnLeft + span) - drawnLeft, 0, 1);
                    }
                    else
                    {
                        // Wider than the canvas: shrunk to it whole, as far as the floor, its leftmost tile at
                        // the edge, and kept as it was rather than replaced by the default.
                        Assert.Equal(-left, dx);
                        Assert.Equal(Math.Max(PanelRigMap.MinScale, width / span), plan.Scale, 9);
                        Assert.Equal(span, plan.Width);
                        Assert.Equal(span * PanelRigMap.MinScale > width + 0.5, plan.Scrolls(width));
                        Assert.Equal((int)kept["Rim"].X, settings.ScreenByNamespace("Rim").LayoutX);
                    }
                }
            }
        }

        [Fact]
        public void A_drop_on_a_shrunk_arrangement_keeps_the_arrangement()
        {
            // Arranged in a 4K window, the two faces side by side; then the window is the artboard's.
            var settings = Rig();
            PanelRigMap.SavePlaces(settings, PanelRigMap.Plan(settings, 3534).Tiles);
            var kept = ById(PanelRigMap.Plan(settings, 3534));
            var narrow = PanelRigMap.Plan(settings, 877);
            Assert.True(narrow.Scale < 1);
            // A nudge of the phone keeps every other tile where the arrangement has it, relative to the rim,
            // and the phone where it was dropped.
            var phone = ById(narrow)["Companion"];
            var dropped = ById(Drop(settings, 877, "Companion", phone.X + 40, phone.Y));
            var after = ById(PanelRigMap.Plan(settings, 877));
            foreach (var id in kept.Keys.Where(id => id != "Companion"))
            {
                Assert.Equal(kept[id].X - kept["Rim"].X, after[id].X - after["Rim"].X);
                Assert.Equal(kept[id].Y, after[id].Y);
            }
            Assert.Equal(new[] { dropped["Companion"].X, dropped["Companion"].Y }, new[] { after["Companion"].X, after["Companion"].Y });
            Assert.Equal(phone.X + 40, after["Companion"].X);
            // Back at 4K it is the arrangement, not the 1200 default pressed into a corner of it.
            var wide = ById(PanelRigMap.Plan(settings, 3534));
            Assert.Equal(kept["MainDash"].X - kept["Rim"].X, wide["MainDash"].X - wide["Rim"].X);
            Assert.Equal(1, PanelRigMap.Plan(settings, 3534).Scale);
        }

        /// <remarks>
        /// The shrunk room was <see cref="PanelRigMap.CanvasHeight"/> over the scale tall, so a drop in its
        /// lower part kept a place far under the arrangement's foot, and every wider window then drew the
        /// canvas that deep at full size: 3,454 px at 4K after one drop in a 700 px control.
        /// </remarks>
        [Fact]
        public void A_drop_at_the_foot_of_a_shrunk_canvas_does_not_deepen_the_canvas_at_full_size()
        {
            var settings = Rig();
            PanelRigMap.SavePlaces(settings, PanelRigMap.Plan(settings, 3517).Tiles);
            var foot = PanelRigMap.Plan(settings, 3517).Tiles.Max(t => t.Y + PanelRigMap.FootprintHeight(t));
            foreach (var narrowWidth in new[] { 877.0, 585 })
            {
                var narrow = PanelRigMap.Plan(settings, narrowWidth);
                Assert.True(narrow.Scale < 1);
                // The room is what a full-size canvas gives the arrangement, drawn shorter than the frame.
                Assert.Equal(Math.Max(PanelRigMap.CanvasHeight, foot), narrow.Height);
                Assert.Equal(PanelRigMap.CanvasHeight, narrow.DrawnHeight);
                Assert.True(narrow.Height * narrow.Scale < narrow.DrawnHeight);
                // Dropped as far down as the room goes.
                Drop(settings, narrowWidth, "Rim", 0, 100000);
                var rim = ById(PanelRigMap.Plan(settings, narrowWidth))["Rim"];
                Assert.True(rim.Y + PanelRigMap.FootprintHeight(rim) <= Math.Max(PanelRigMap.CanvasHeight, foot));
                var wide = PanelRigMap.Plan(settings, 3517);
                Assert.True(wide.Height <= Math.Max(PanelRigMap.CanvasHeight, foot), narrowWidth + ": " + wide.Height);
                Assert.Equal(PanelRigMap.CanvasHeight, wide.DrawnHeight);
            }
        }

        [Fact]
        public void An_arrangement_is_never_shrunk_past_legibility_and_scrolls_across_instead()
        {
            Assert.Equal(0.7, PanelRigMap.MinScale);
            // The phone at the left edge of a 4K canvas and the pit wall at its right.
            var settings = Rig();
            var at4k = ById(PanelRigMap.Plan(settings, 3517));
            Drop(settings, 3517, "Companion", 0, at4k["Companion"].Y);
            Drop(settings, 3517, "PitWall", 3517, at4k["PitWall"].Y);
            var kept = PanelRigMap.Plan(settings, 3517);
            Inside(kept, 3517, "4K");
            Assert.False(kept.Scrolls(3517));
            var span = kept.Tiles.Max(t => t.X + PanelRigMap.FootprintWidth(t)) - kept.Tiles.Min(t => t.X);
            Assert.True(span > 3400);
            foreach (var width in new[] { 2600.0, 1377, 877, 585 })
            {
                var plan = PanelRigMap.Plan(settings, width);
                Inside(plan, width, width.ToString());
                // Never under the floor: a name is drawn at 8.4 px at the least, not 2.
                Assert.Equal(Math.Max(PanelRigMap.MinScale, width / span), plan.Scale, 9);
                Assert.True(PanelRigMap.NameSize * plan.Scale >= 8.4 - 1e-9);
                Assert.Equal(span * PanelRigMap.MinScale > width + 0.5, plan.Scrolls(width));
                Assert.Equal(span * plan.Scale, plan.DrawnWidth, 6);
            }
            // A drop on the room that scrolls keeps the arrangement, and lands inside the room.
            var phone = ById(PanelRigMap.Plan(settings, 585))["Companion"];
            Drop(settings, 585, "Companion", phone.X + 40, phone.Y);
            var after = PanelRigMap.Plan(settings, 585);
            Inside(after, 585, "after");
            Assert.Equal(ById(kept)["PitWall"].X - ById(kept)["Rim"].X, ById(after)["PitWall"].X - ById(after)["Rim"].X);
        }

        /// <remarks>
        /// A recorded decision rather than an accident, until the settings keep the width an arrangement was
        /// kept at: an arrangement is kept in absolute pixels, so one kept in a narrower window is drawn at the
        /// left of a wider canvas, where the default centres (A_wide_window_centres_the_rig...).
        /// </remarks>
        [Fact]
        public void An_arrangement_kept_in_a_narrow_window_is_drawn_where_it_was_kept_in_a_wider_one()
        {
            var settings = Rig();
            var phone = ById(PanelRigMap.Plan(settings, 877))["Companion"];
            var kept = Drop(settings, 877, "Companion", phone.X, phone.Y + 20);
            var wide = PanelRigMap.Plan(settings, 3517);
            Assert.Equal(1, wide.Scale);
            Assert.All(wide.Tiles, t => Assert.Equal(new[] { ById(kept)[t.Id].X, ById(kept)[t.Id].Y }, new[] { t.X, t.Y }));
            var left = wide.Tiles.Min(t => t.X);
            var right = wide.Tiles.Max(t => t.X + PanelRigMap.FootprintWidth(t));
            Assert.True(left < 3517 - right, "kept at the left");
        }

        [Fact]
        public void A_rig_arranged_in_a_narrow_window_is_not_clamped_onto_the_foot_of_a_wide_one()
        {
            var settings = Rig();
            // The fallback's canvas is taller than the 580 at 600.
            Assert.True(PanelRigMap.Plan(settings, 600).Height > PanelRigMap.CanvasHeight);
            Drop(settings, 600, "PitWall", 360, 480);
            var narrow = Drop(settings, 600, "Companion", 400, 660);
            var wall = ById(narrow)["PitWall"];
            var phone = ById(narrow)["Companion"];
            Assert.False(Overlap(wall, phone));

            var wide = PanelRigMap.Plan(settings, 1100);
            Inside(wide, 1100, "1100");
            Assert.Equal(new[] { wall.X, wall.Y }, new[] { ById(wide)["PitWall"].X, ById(wide)["PitWall"].Y });
            Assert.Equal(new[] { phone.X, phone.Y }, new[] { ById(wide)["Companion"].X, ById(wide)["Companion"].Y });
            Assert.False(Overlap(ById(wide)["PitWall"], ById(wide)["Companion"]));
            // The canvas holds the lowest, rather than being 580 and clamping it.
            Assert.True(wide.Height > PanelRigMap.CanvasHeight);
            Assert.Equal(narrow.Tiles.Max(t => t.Y + PanelRigMap.FootprintHeight(t)), wide.Height);
            Assert.Subset(Overlaps(narrow), Overlaps(wide));
        }

        [Fact]
        public void A_phone_dropped_under_the_rim_stays_under_it_when_the_window_widens()
        {
            var settings = Rig();
            var rim = ById(PanelRigMap.Plan(settings, 894))["Rim"];
            var narrow = ById(Drop(settings, 894, "Companion", rim.X, rim.Y + PanelRigMap.FootprintHeight(rim) + 40));
            var wide = ById(PanelRigMap.Plan(settings, 1100));
            Assert.Equal(narrow["Companion"].X - narrow["Rim"].X, wide["Companion"].X - wide["Rim"].X);
            Assert.Equal(narrow["Companion"].Y - narrow["Rim"].Y, wide["Companion"].Y - wide["Rim"].Y);
        }

        [Fact]
        public void A_device_added_after_the_rig_was_arranged_goes_under_the_arrangement()
        {
            var settings = Rig();
            var arranged = Drop(settings, 1100, "Rim", 700, 300);
            settings.AddLedBar("4-12-4", "Pedals", LedBar.ArduinoDevice);
            var plan = PanelRigMap.Plan(settings, 1100);
            Inside(plan, 1100, "1100");
            var added = plan.Tiles.Single(t => t.Id == "led:LedPedals");
            var foot = arranged.Tiles.Max(t => t.Y + PanelRigMap.FootprintHeight(t));
            Assert.Equal(PanelRigMap.LayoutMargin, added.X);
            Assert.Equal(foot + PanelRigMap.LayoutGap, added.Y);
            Assert.Equal(added.Y + PanelRigMap.FootprintHeight(added) + PanelRigMap.HintClear, plan.Height);
            Assert.DoesNotContain(Overlaps(plan), pair => pair.Contains("led:LedPedals"));
            foreach (var tile in arranged.Tiles) Assert.Equal(new[] { tile.X, tile.Y }, new[] { ById(plan)[tile.Id].X, ById(plan)[tile.Id].Y });
        }

        [Fact]
        public void A_device_added_under_a_shrunk_arrangement_clears_the_hint_as_drawn()
        {
            // Arranged in a 4K window and planned in the artboard's: the room is drawn shrunk, so the hint's
            // clearance is HintClear on the screen, which is more of the room's own pixels.
            var settings = Rig();
            PanelRigMap.SavePlaces(settings, PanelRigMap.Plan(settings, 3534).Tiles);
            settings.AddLedBar("4-12-4", "Pedals", LedBar.ArduinoDevice);
            var plan = PanelRigMap.Plan(settings, 877);
            Assert.True(plan.Scale < 1);
            Inside(plan, 877, "877");
            var added = plan.Tiles.Single(t => t.Id == "led:LedPedals");
            Assert.Equal(added.Y + PanelRigMap.FootprintHeight(added) + PanelRigMap.HintClear / plan.Scale, plan.Height, 9);
        }

        [Fact]
        public void A_screen_tile_is_the_screen_a_drop_keeps()
        {
            var settings = Rig();
            var tiles = PanelRigMap.Tiles(settings).ToDictionary(t => t.Id);
            Assert.Same(settings.ScreenByNamespace("Rim"), PanelRigMap.ScreenOf(settings, tiles["Rim"]));
            Assert.Same(settings.ScreenByNamespace("Slots480x480"), PanelRigMap.ScreenOf(settings, tiles["Slots480x480"]));
            Assert.Null(PanelRigMap.ScreenOf(settings, tiles["led:LedWheelRim"]));
            Assert.Null(PanelRigMap.ScreenOf(settings, tiles["matrix:1"]));
            Assert.Null(PanelRigMap.ScreenOf(null, tiles["Rim"]));
        }

        [Fact]
        public void A_negative_saved_place_is_no_place()
        {
            var settings = Rig();
            var tiles = PanelRigMap.Tiles(settings).ToDictionary(t => t.Id);
            int x, y;
            settings.ScreenByNamespace("Rim").LayoutX = -20;
            settings.ScreenByNamespace("Rim").LayoutY = 40;
            Assert.False(PanelRigMap.TrySaved(settings, tiles["Rim"], out x, out y));
            settings.LedBarByNamespace("LedWheelRim").LayoutX = 40;
            settings.LedBarByNamespace("LedWheelRim").LayoutY = -1;
            Assert.False(PanelRigMap.TrySaved(settings, tiles["led:LedWheelRim"], out x, out y));
            settings.MatrixLayoutX[0] = 0;
            settings.MatrixLayoutY[0] = 0;
            Assert.True(PanelRigMap.TrySaved(settings, tiles["matrix:1"], out x, out y));
        }

        [Fact]
        public void Reset_layout_forgets_every_place_a_tile_was_put()
        {
            var settings = Rig();
            var tiles = PanelRigMap.Tiles(settings);
            Assert.False(PanelRigMap.ClearLayout(settings));
            PanelRigMap.SavePlaces(settings, tiles.Select(tile => tile.At(100, 100)));
            Assert.All(tiles, tile => { int x, y; Assert.True(PanelRigMap.TrySaved(settings, tile, out x, out y)); });
            Assert.True(PanelRigMap.ClearLayout(settings));
            Assert.All(tiles, tile => { int x, y; Assert.False(PanelRigMap.TrySaved(settings, tile, out x, out y)); });
            // Both halves of every place: a half left behind is a setting the file keeps for nothing.
            Assert.All(settings.RigScreens(), s => { Assert.Null(s.LayoutX); Assert.Null(s.LayoutY); });
            Assert.All(settings.LedBarList(), b => { Assert.Null(b.LayoutX); Assert.Null(b.LayoutY); });
            Assert.All(settings.MatrixLayoutX, v => Assert.Null(v));
            Assert.All(settings.MatrixLayoutY, v => Assert.Null(v));
            // A matrix with only its Y left is still forgotten.
            settings.MatrixLayoutY[1] = 40;
            Assert.True(PanelRigMap.ClearLayout(settings));
            Assert.Null(settings.MatrixLayoutY[1]);
            Assert.False(PanelRigMap.ClearLayout(settings));
            var placed = PanelRigMap.Plan(settings, 1100).Tiles;
            Assert.Equal(PanelRigMap.Plan(Rig(), 1100).Tiles.Select(t => t.X + "," + t.Y), placed.Select(t => t.X + "," + t.Y));
        }

        [Fact]
        public void A_device_on_homes_list_carries_the_warning_dot()
        {
            var tiles = PanelRigMap.Tiles(Rig()).ToDictionary(t => t.Id);
            PanelIssue Issue(string rule, string subject)
            {
                return new PanelIssue(rule + (subject ?? string.Empty), PanelPage.Home, subject, "t", null, new List<string[]>(), "a", PanelIssueAction.Navigate);
            }
            var issues = new List<PanelIssue>
            {
                Issue(PanelAttention.StripUnselected, "LedDashBrow"),
                Issue(PanelAttention.MatrixDark, "1"),
                Issue(PanelAttention.ScreenMissing, "Rim"),
            };
            Assert.True(PanelRigMap.Warns(tiles["led:LedDashBrow"], issues));
            Assert.False(PanelRigMap.Warns(tiles["led:LedWheelRim"], issues));
            Assert.True(PanelRigMap.Warns(tiles["matrix:1"], issues));
            Assert.False(PanelRigMap.Warns(tiles["matrix:2"], issues));
            Assert.True(PanelRigMap.Warns(tiles["Rim"], issues));
            Assert.False(PanelRigMap.Warns(tiles["MainDash"], issues));
            Assert.True(PanelRigMap.Warns(tiles["led:LedWheelRim"], new[] { Issue(PanelAttention.StripOutdated, "LedWheelRim") }));
            Assert.True(PanelRigMap.Warns(tiles["matrix:2"], new[] { Issue(PanelAttention.FlagBoxOutdated, null) }));
            Assert.True(PanelRigMap.Warns(tiles["MainDash"], new[] { Issue(PanelAttention.ScreenRestart, "MainDash") }));
            Assert.False(PanelRigMap.Warns(tiles["MainDash"], null));
        }

        [Fact]
        public void A_face_shows_the_pages_its_zones_are_on_and_its_band_under_them()
        {
            var screen = Screen(Contract.KindFace, "MainDash", "Main dash", 1280, 480);
            var zones = PanelRigMap.FaceZones(screen);
            // A wide face draws B, A, C; zone A on its gear page is the gear.
            Assert.Equal(new[] { "B", "A", "C" }, zones.Select(z => z.Letter));
            Assert.Equal(new[] { false, true, false }, zones.Select(z => z.Gear));
            Assert.Equal("4", zones[1].Text);
            Assert.Equal(FacePages.NameOf("B", Contract.DefaultFaceZonePages[1]), zones[0].Text);
            Assert.Equal(FacePages.NameOf("C", Contract.DefaultFaceZonePages[2]), zones[2].Text);
            Assert.Equal(new double[] { 469, 340, 469 }, zones.Select(z => z.Weight));
            Assert.False(PanelRigMap.FaceColumn(screen));

            // The page a zone is on now, not the one it opens on; the page reads FaceState on its clock and
            // repaints the tile when a wheel button moves one.
            screen.Face = new FaceSettings();
            screen.Face.Normalise();
            var before = PanelRigMap.FaceState(screen);
            screen.Face.Zones[0] = 2;
            screen.Face.Zones[3] = 3;
            Assert.NotEqual(before, PanelRigMap.FaceState(screen));
            Assert.Equal("2," + screen.Face.Zone("B") + "," + screen.Face.Zone("C") + ",3", PanelRigMap.FaceState(screen));
            zones = PanelRigMap.FaceZones(screen);
            Assert.Equal("Speed", zones[1].Text);
            Assert.False(zones[1].Gear);
            Assert.Equal(string.Empty, PanelRigMap.FaceState(null));

            // Zone A's "Gear alone" is the gear too.
            screen.Face.Zones[0] = 1;
            Assert.Equal("gearAlone", FacePages.IdOf("A", 1));
            zones = PanelRigMap.FaceZones(screen);
            Assert.True(zones[1].Gear);
            Assert.Equal("4", zones[1].Text);

            var tall = Screen(Contract.KindFace, "Tall", "Tall", 600, 686);
            Assert.True(PanelRigMap.FaceColumn(tall));
            Assert.Equal(new[] { "A", "B", "C" }, PanelRigMap.FaceZones(tall).Select(z => z.Letter));
        }

        [Fact]
        public void A_faces_band_and_gear_scale_with_it_as_the_artboard_draws_its_two()
        {
            Assert.Equal(24, PanelRigMap.BandHeight(135));
            Assert.Equal(20, PanelRigMap.BandHeight(112));
            Assert.Equal(12, PanelRigMap.BandHeight(40));
            Assert.Equal(44, PanelRigMap.FaceGearSize(135, false));
            Assert.Equal(36, PanelRigMap.FaceGearSize(112, false));
            Assert.Equal(29, PanelRigMap.FaceGearSize(180, true));
        }

        [Fact]
        public void The_revs_and_the_ring_follow_the_scenario_unless_the_rev_bar_is_off()
        {
            Assert.Equal(PanelEmulation.Revs(20, PanelEmulation.Mid), PanelRigMap.FaceRevs(PanelEmulation.Mid, Contract.RevBarShift));
            var off = PanelRigMap.FaceRevs(PanelEmulation.Shift, Contract.RevBarOff);
            Assert.Equal(20, off.Length);
            Assert.All(off, led => Assert.Null(led));
            Assert.Equal(Theme.ShiftStage3, PanelRigMap.RingColour(PanelEmulation.Shift, Contract.RevBarShift));
            Assert.Equal(Theme.ShiftStage1, PanelRigMap.RingColour(PanelEmulation.Mid, Contract.RevBarShift));
            Assert.Equal(Theme.SurfaceRaised, PanelRigMap.RingColour(PanelEmulation.Shift, Contract.RevBarOff));
        }

        [Fact]
        public void A_face_follows_its_own_rev_bar_and_the_round_the_rigs()
        {
            var settings = Rig();
            settings.SetRevBar(Contract.RevBarShift);
            var main = settings.ScreenByNamespace("MainDash");
            Assert.Equal(PanelEmulation.Revs(20, PanelEmulation.Shift), PanelRigMap.FaceRevs(PanelEmulation.Shift, settings, main));
            // The face's own Revbar off: its strip is unlit while the rig's is on.
            settings.SetScreenRevBar("MainDash", Contract.RevBarOff);
            Assert.All(PanelRigMap.FaceRevs(PanelEmulation.Shift, settings, main), led => Assert.Null(led));
            Assert.Equal(PanelEmulation.Revs(20, PanelEmulation.Shift), PanelRigMap.FaceRevs(PanelEmulation.Shift, settings, settings.ScreenByNamespace("Rim")));
            Assert.Equal(Theme.ShiftStage3, PanelRigMap.RingFor(PanelEmulation.Shift, settings).Hex);
            // The rig's off: the round's ring goes dark, and a face with no Revbar of its own follows it.
            settings.SetRevBar(Contract.RevBarOff);
            Assert.Equal(Theme.SurfaceRaised, PanelRigMap.RingFor(PanelEmulation.Shift, settings).Hex);
            Assert.All(PanelRigMap.FaceRevs(PanelEmulation.Shift, settings, settings.ScreenByNamespace("Rim")), led => Assert.Null(led));
            // A face the rig no longer has follows the rig.
            Assert.Equal(Contract.RevBarOff, PanelRigMap.FaceRevBar(settings, null));
        }

        [Fact]
        public void A_round_faces_ring_is_its_flag_ring_while_a_flag_is_picked()
        {
            var yellow = PanelRigMap.RingFor(PanelEmulation.Yellow, Contract.RevBarShift);
            Assert.Equal(Theme.FlagYellow, yellow.Hex);
            Assert.Equal(PanelRigMap.RingThickness, yellow.Thickness);
            Assert.False(yellow.Chequer);
            // Even with the rev ring off: the flag ring is not the revs.
            Assert.Equal(Theme.FlagBlue, PanelRigMap.RingFor(PanelEmulation.Blue, Contract.RevBarOff).Hex);
            // The black flag an outline in the text colour, the chequer as checks.
            var black = PanelRigMap.RingFor(PanelEmulation.Black, Contract.RevBarShift);
            Assert.Equal(Theme.TextPrimary, black.Hex);
            Assert.Equal(PanelRigMap.RingOutline, black.Thickness);
            Assert.True(PanelRigMap.RingFor(PanelEmulation.Chequer, Contract.RevBarShift).Chequer);
            // Anything else is the revs.
            Assert.Equal(Theme.ShiftStage1, PanelRigMap.RingFor(PanelEmulation.Limiter, Contract.RevBarShift).Hex);
            Assert.Equal(Theme.ShiftStage3, PanelRigMap.RingFor(PanelEmulation.Shift, Contract.RevBarShift).Hex);
        }

        [Fact]
        public void A_flag_fills_the_phone_and_nothing_else_does()
        {
            var full = PanelRigMap.FlagFormatFull;
            Assert.Equal(Theme.FlagYellow, PanelRigMap.CompanionBand(PanelEmulation.Yellow, full).FillHex);
            Assert.Equal("YELLOW", PanelRigMap.CompanionBand(PanelEmulation.Yellow, full).Text);
            // The full-screen block's one word, never the band's "WHITE · LAST LAP".
            Assert.Equal("WHITE", PanelRigMap.CompanionBand(PanelEmulation.White, full).Text);
            Assert.True(PanelRigMap.CompanionBand(PanelEmulation.Black, full).Outlined);
            Assert.True(PanelRigMap.CompanionBand(PanelEmulation.Chequer, full).Chequer);
            Assert.Null(PanelRigMap.CompanionBand(PanelEmulation.Chequer, full).Text);
            Assert.False(PanelRigMap.CompanionBand(PanelEmulation.Limiter, full).Alert);
            Assert.False(PanelRigMap.CompanionBand(PanelEmulation.CarLeft, full).Alert);
            var phone = Screen(Contract.KindCompanion, "Companion", "Phone", 850, 480);
            Assert.Equal(Modules.All[Contract.DefaultCompanionStart].Name, PanelRigMap.CompanionIdle(phone));
            phone.CompanionStart = 3;
            Assert.Equal(Modules.All[3].Name, PanelRigMap.CompanionIdle(phone));
            // A start the modules do not have is the default's, rather than an index past the list.
            foreach (var start in new[] { -1, Modules.All.Count })
            {
                phone.CompanionStart = start;
                Assert.Equal(Modules.All[Contract.DefaultCompanionStart].Name, PanelRigMap.CompanionIdle(phone));
            }
            Assert.Equal(Modules.All[Contract.DefaultCompanionStart].Name, PanelRigMap.CompanionIdle(null));
        }

        [Fact]
        public void The_block_names_a_flag_as_the_dashs_full_screen_block_does()
        {
            Assert.Equal(new[] { "GREEN", "YELLOW", "BLUE", "WHITE", "BLACK", null, "RED" },
                new[] { PanelEmulation.Green, PanelEmulation.Yellow, PanelEmulation.Blue, PanelEmulation.White, PanelEmulation.Black, PanelEmulation.Chequer, PanelEmulation.Red }.Select(PanelRigMap.BlockName));
            Assert.Null(PanelRigMap.BlockName(PanelEmulation.Limiter));
            Assert.False(PanelRigMap.BlockFor(PanelEmulation.Limiter).Alert);
            Assert.Equal(Theme.FlagWhite, PanelRigMap.BlockFor(PanelEmulation.White).FillHex);
        }

        [Fact]
        public void Each_screen_draws_a_flag_by_its_own_flag_format()
        {
            // The formats are the contract's own.
            Assert.Equal(new[] { PanelRigMap.FlagFormatBand, PanelRigMap.FlagFormatFull }, Contract.FlagFormats);
            Assert.Equal(new[] { PanelRigMap.FlagFormatOff, PanelRigMap.FlagFormatBand, PanelRigMap.FlagFormatFull }, Contract.CompanionFlagFormats);

            var settings = Rig();
            var main = settings.ScreenByNamespace("MainDash");
            var wall = settings.ScreenByNamespace("PitWall");
            var phone = settings.ScreenByNamespace("Companion");
            var yellow = PanelEmulation.Yellow;

            // A face: on band D by default; over zones B, A and C in full, band D left to its page.
            Assert.Equal(PanelRigMap.FlagFormatBand, PanelRigMap.FaceFlagFormat(settings, main));
            Assert.Equal("YELLOW", PanelRigMap.FaceBandFor(yellow, PanelRigMap.FaceFlagFormat(settings, main)).Text);
            Assert.False(PanelRigMap.FaceBlockFor(yellow, PanelRigMap.FaceFlagFormat(settings, main)).Alert);
            main.FlagFormat = PanelRigMap.FlagFormatFull;
            Assert.False(PanelRigMap.FaceBandFor(yellow, PanelRigMap.FaceFlagFormat(settings, main)).Alert);
            Assert.Equal("YELLOW", PanelRigMap.FaceBlockFor(yellow, PanelRigMap.FaceFlagFormat(settings, main)).Text);
            Assert.Equal("WHITE", PanelRigMap.FaceBlockFor(PanelEmulation.White, PanelRigMap.FlagFormatFull).Text);
            Assert.Equal("WHITE · LAST LAP", PanelRigMap.FaceBandFor(PanelEmulation.White, PanelRigMap.FlagFormatBand).Text);
            // The limiter is never on band D: the dash draws it over zone A (LimiterPaint).
            Assert.False(PanelRigMap.FaceBandFor(PanelEmulation.Limiter, PanelRigMap.FlagFormatFull).Alert);
            Assert.False(PanelRigMap.FaceBandFor(PanelEmulation.Limiter, PanelRigMap.FlagFormatBand).Alert);
            Assert.False(PanelRigMap.FaceBlockFor(PanelEmulation.Limiter, PanelRigMap.FlagFormatFull).Alert);
            // Nothing but a flag takes band D.
            foreach (var id in PanelEmulation.Scenarios().Select(s => s.Id).Where(id => !PanelEmulation.IsFlag(id)))
            {
                Assert.False(PanelRigMap.FaceBandFor(id, PanelRigMap.FlagFormatBand).Alert, id);
            }

            // A pit wall: a band under its header by default, a block over its body in full, nothing when off.
            Assert.Equal(PanelRigMap.FlagFormatBand, PanelRigMap.PitWallFlagFormat(settings, wall));
            Assert.Equal("YELLOW", PanelRigMap.PitWallBandFor(yellow, PanelRigMap.PitWallFlagFormat(settings, wall)).Text);
            Assert.False(PanelRigMap.PitWallBlockFor(yellow, PanelRigMap.PitWallFlagFormat(settings, wall)).Alert);
            wall.PitWallFlagFormat = PanelRigMap.FlagFormatFull;
            Assert.False(PanelRigMap.PitWallBandFor(yellow, PanelRigMap.PitWallFlagFormat(settings, wall)).Alert);
            Assert.Equal("YELLOW", PanelRigMap.PitWallBlockFor(yellow, PanelRigMap.PitWallFlagFormat(settings, wall)).Text);
            wall.PitWallFlagFormat = PanelRigMap.FlagFormatOff;
            Assert.False(PanelRigMap.PitWallBandFor(yellow, PanelRigMap.PitWallFlagFormat(settings, wall)).Alert);
            Assert.False(PanelRigMap.PitWallBlockFor(yellow, PanelRigMap.PitWallFlagFormat(settings, wall)).Alert);
            // The pit wall's band is a flag strip: the limiter, a face's alone, leaves it empty.
            Assert.False(PanelRigMap.PitWallBandFor(PanelEmulation.Limiter, PanelRigMap.FlagFormatBand).Alert);
            Assert.Null(PanelRigMap.BandPaint(PanelRigMap.PitWallBandFor(PanelEmulation.Limiter, PanelRigMap.FlagFormatBand), 230).Words);

            // A phone: filled by default; a colour-only strip at its foot as a band; nothing when off.
            Assert.Equal(PanelRigMap.FlagFormatFull, PanelRigMap.CompanionFlagFormat(settings, phone));
            Assert.True(PanelRigMap.CompanionBand(yellow, PanelRigMap.CompanionFlagFormat(settings, phone)).Alert);
            Assert.False(PanelRigMap.CompanionStrip(yellow, PanelRigMap.CompanionFlagFormat(settings, phone)).Alert);
            phone.CompanionFlagFormat = PanelRigMap.FlagFormatBand;
            Assert.False(PanelRigMap.CompanionBand(yellow, PanelRigMap.CompanionFlagFormat(settings, phone)).Alert);
            var strip = PanelRigMap.StripPaint(PanelRigMap.CompanionStrip(yellow, PanelRigMap.CompanionFlagFormat(settings, phone)));
            Assert.Equal(Theme.FlagYellow, strip.FillHex);
            Assert.Null(strip.Words);
            phone.CompanionFlagFormat = PanelRigMap.FlagFormatOff;
            Assert.False(PanelRigMap.CompanionBand(yellow, PanelRigMap.CompanionFlagFormat(settings, phone)).Alert);
            Assert.Null(PanelRigMap.StripPaint(PanelRigMap.CompanionStrip(yellow, PanelRigMap.CompanionFlagFormat(settings, phone))));

            // A screen the rig no longer has takes the contract's defaults.
            Assert.Equal(Contract.DefaultFlagFormat, PanelRigMap.FaceFlagFormat(settings, null));
            Assert.Equal(Contract.DefaultPitWallFlagFormat, PanelRigMap.PitWallFlagFormat(settings, null));
            Assert.Equal(Contract.DefaultCompanionFlagFormat, PanelRigMap.CompanionFlagFormat(settings, null));
        }

        [Fact]
        public void A_band_a_block_and_a_phone_are_painted_as_the_dash_paints_them()
        {
            // A band nothing takes: the zone ground, drawn empty at rest, as docs/design/plugin.md records
            // against the artboard's "Fuel · 12.4 L" -- never a value, never the page's name, never "Band D".
            var idle = PanelRigMap.BandPaint(FaceBand.Idle, 290);
            Assert.Null(idle.Words);
            Assert.Equal(Theme.SurfaceZone, idle.FillHex);
            Assert.Equal(Theme.TextSecondary, idle.InkHex);
            Assert.Null(idle.BorderHex);
            Assert.Null(PanelRigMap.BandPaint(null, 290).Words);
            // A flag on its colour, its words tracked.
            var yellow = PanelRigMap.BandPaint(PanelEmulation.Band(PanelEmulation.Yellow), 290);
            Assert.Equal("YELLOW", yellow.Words);
            Assert.Equal(Theme.FlagYellow, yellow.FillHex);
            Assert.Equal(Theme.OnFlag, yellow.InkHex);
            Assert.Null(yellow.BorderHex);
            Assert.True(yellow.Tracked);
            Assert.False(yellow.RuleOnTop);
            // The black flag an outline of its colour on the base ground.
            var black = PanelRigMap.BandPaint(PanelEmulation.Band(PanelEmulation.Black), 290);
            Assert.Equal("BLACK", black.Words);
            Assert.Equal(Theme.SurfaceBase, black.FillHex);
            Assert.Equal(Theme.FlagBlack, black.BorderHex);
            Assert.Equal(PanelRigMap.BandOutline, black.BorderWidth);
            Assert.True(black.Tracked);
            Assert.False(black.RuleOnTop);
            // The chequer says nothing.
            var chequer = PanelRigMap.BandPaint(PanelEmulation.Band(PanelEmulation.Chequer), 290);
            Assert.True(chequer.Chequer);
            Assert.Null(chequer.Words);
            Assert.Null(chequer.FillHex);
            Assert.False(chequer.Tracked);

            // A phone nothing takes: the module it opens on, wrapped, on the inset ground inside its border.
            var phone = PanelRigMap.CompanionPaint(FaceBand.Idle, "Timing");
            Assert.Equal("Timing", phone.Words);
            Assert.False(phone.Tracked);
            Assert.Equal(Theme.SurfaceInset, phone.FillHex);
            Assert.Equal(Theme.Border, phone.BorderHex);
            // A flag over it, its one word tracked; the black flag a 3 px outline; the chequer with no word.
            var white = PanelRigMap.CompanionPaint(PanelRigMap.BlockFor(PanelEmulation.White), "Timing");
            Assert.Equal("WHITE", white.Words);
            Assert.True(white.Tracked);
            Assert.Equal(Theme.FlagWhite, white.FillHex);
            var outlined = PanelRigMap.CompanionPaint(PanelRigMap.BlockFor(PanelEmulation.Black), "Timing");
            Assert.Equal(Theme.FlagBlack, outlined.BorderHex);
            Assert.Equal(PanelRigMap.CompanionOutline, outlined.BorderWidth);
            Assert.Equal(Theme.SurfaceBase, outlined.FillHex);
            var chequered = PanelRigMap.CompanionPaint(PanelRigMap.BlockFor(PanelEmulation.Chequer), "Timing");
            Assert.True(chequered.Chequer);
            Assert.Null(chequered.Words);

            // A strip nothing takes is not drawn.
            Assert.Null(PanelRigMap.StripPaint(FaceBand.Idle));
        }

        [Fact]
        public void The_limiter_and_the_low_fuel_pop_up_are_drawn_over_zone_a_as_the_dash_draws_them()
        {
            // The limiter: the dash's neutral block, its words in the base ground, untracked (pitLimiter.ts).
            var limiter = PanelRigMap.LimiterPaint(PanelEmulation.Limiter, 80);
            Assert.Equal("Pit limiter", limiter.Words);
            Assert.Equal(Theme.PitLimiter, limiter.FillHex);
            Assert.Equal(Theme.SurfaceBase, limiter.InkHex);
            Assert.False(limiter.Tracked);
            Assert.Null(limiter.BorderHex);
            // Too narrow for its words: the block alone, never the words cut.
            Assert.Null(PanelRigMap.LimiterPaint(PanelEmulation.Limiter, 30).Words);
            Assert.Equal(Theme.PitLimiter, PanelRigMap.LimiterPaint(PanelEmulation.Limiter, 30).FillHex);
            // The low-fuel pop-up: the pop-up's ground and rule along its top, its one word in fuel.low.
            var fuel = PanelRigMap.PopUpPaint(PanelEmulation.LowFuel);
            Assert.Equal("Fuel", fuel.Words);
            Assert.Equal(Theme.SurfaceZone, fuel.FillHex);
            Assert.Equal(Theme.Danger, fuel.InkHex);
            Assert.Equal(Theme.TextPrimary, fuel.BorderHex);
            Assert.Equal(PanelRigMap.PopUpRule, fuel.BorderWidth);
            Assert.True(fuel.RuleOnTop);
            // Nothing else takes zone A.
            foreach (var id in PanelEmulation.Scenarios().Select(s => s.Id))
            {
                Assert.Equal(id == PanelEmulation.Limiter, PanelRigMap.LimiterPaint(id, 80) != null);
                Assert.Equal(id == PanelEmulation.LowFuel, PanelRigMap.PopUpPaint(id) != null);
            }
            Assert.Equal(12, PanelRigMap.LimiterHeight);
            Assert.Equal(56, PanelRigMap.RoundLimiterWidth);
            Assert.Equal(18, PanelRigMap.RoundLimiterTop);
            Assert.Equal(2, PanelRigMap.PopUpRule);

            // Zone A's width: its share of the body less the gap before it, or the whole body when stacked.
            var wide = Screen(Contract.KindFace, "MainDash", "Main dash", 1280, 480);
            var zones = PanelRigMap.FaceZones(wide);
            var inner = PanelRigMap.ScreenInner(300);
            Assert.Equal(Math.Floor(inner * 340 / 1278 - 3), PanelRigMap.ZoneWidth(zones, "A", false, inner));
            Assert.Equal(Math.Floor(inner * 469 / 1278), PanelRigMap.ZoneWidth(zones, "B", false, inner));
            Assert.Equal(inner, PanelRigMap.ZoneWidth(zones, "A", true, inner));
            Assert.Equal(0, PanelRigMap.ZoneWidth(zones, "D", false, inner));
            Assert.Equal(0, PanelRigMap.ZoneWidth(null, "A", false, inner));

            // "Pit limiter" is drawn on the zone A of every face the rig can draw a row of zones on, and
            // inside the round's banner.
            Assert.NotNull(PanelRigMap.LimiterPaint(PanelEmulation.Limiter, PanelRigMap.RoundLimiterWidth).Words);
            foreach (var size in Contract.FaceSizes)
            {
                double w, h;
                PanelRigMap.FaceTileSize(size.Width, size.Height, out w, out h);
                var screen = Screen(Contract.KindFace, "F", "F", size.Width, size.Height);
                var room = PanelRigMap.ZoneWidth(PanelRigMap.FaceZones(screen), "A", PanelRigMap.FaceColumn(screen), PanelRigMap.ScreenInner(w));
                var words = PanelRigMap.LimiterPaint(PanelEmulation.Limiter, room).Words;
                Assert.True(words != null, "no words on " + size.Width + "x" + size.Height);
                Assert.True(PanelRigMap.TrackedWidth(words, PanelRigMap.BandTextSize, 0) <= room, size.Width + "x" + size.Height);
            }
            var reference = PanelRigMap.ZoneWidth(zones, "A", false, inner);
            Assert.Equal("Pit limiter", PanelRigMap.LimiterPaint(PanelEmulation.Limiter, reference).Words);
        }

        /// <summary>A TrueType file's advance for each character, in em: head's em, hhea's metric count,
        /// hmtx's widths and a format 4 cmap, which is what tools/measure-font reads too.</summary>
        private static IDictionary<char, double> FontAdvances(string path, IEnumerable<char> characters)
        {
            var font = File.ReadAllBytes(path);
            int U16(int at) => (font[at] << 8) | font[at + 1];
            long U32(int at) => ((long)font[at] << 24) | ((long)font[at + 1] << 16) | ((long)font[at + 2] << 8) | font[at + 3];
            var tables = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < U16(4); i++)
            {
                var record = 12 + 16 * i;
                tables[System.Text.Encoding.ASCII.GetString(font, record, 4)] = (int)U32(record + 8);
            }
            var em = U16(tables["head"] + 18);
            var metrics = U16(tables["hhea"] + 34);
            var cmap = tables["cmap"];
            var sub = -1;
            for (var i = 0; i < U16(cmap + 2); i++)
            {
                var record = cmap + 4 + 8 * i;
                var at = cmap + (int)U32(record + 4);
                if (U16(at) == 4 && (U16(record) == 3 || U16(record) == 0)) { sub = at; break; }
            }
            Assert.True(sub >= 0, "no format 4 cmap in " + path);
            var segments = U16(sub + 6) / 2;
            var ends = sub + 14;
            var starts = ends + 2 * segments + 2;
            var deltas = starts + 2 * segments;
            var offsets = deltas + 2 * segments;
            var advances = new Dictionary<char, double>();
            foreach (var character in characters)
            {
                var code = (int)character;
                var glyph = 0;
                for (var s = 0; s < segments; s++)
                {
                    if (code > U16(ends + 2 * s)) continue;
                    var start = U16(starts + 2 * s);
                    if (code < start) break;
                    var delta = U16(deltas + 2 * s);
                    var offset = U16(offsets + 2 * s);
                    if (offset == 0) glyph = (code + delta) & 0xFFFF;
                    else
                    {
                        var at = offsets + 2 * s + offset + 2 * (code - start);
                        glyph = U16(at);
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }
                    break;
                }
                Assert.True(glyph != 0, "no glyph for '" + character + "'");
                var advance = U16(tables["hmtx"] + 4 * Math.Min(glyph, metrics - 1));
                advances[character] = (double)advance / em;
            }
            return advances;
        }

        [Fact]
        public void A_bands_words_are_measured_in_the_weight_they_are_drawn_in()
        {
            // The table is Barlow SemiBold's, held here to the font file the panel embeds, entry for entry.
            var path = Path.Combine(RepoPaths.Root(), "packages", "dash", "fonts", "Barlow-SemiBold.ttf");
            var glyphs = PanelRigMap.BandGlyphs.ToList();
            Assert.Contains('·', glyphs);
            Assert.Equal(26 * 2 + 2, glyphs.Count);
            var font = FontAdvances(path, glyphs);
            foreach (var character in glyphs) Assert.Equal(Math.Round(font[character], 3), PanelRigMap.BandAdvance(character), 3);
            // A character it does not carry is measured as its widest.
            Assert.Equal(glyphs.Max(PanelRigMap.BandAdvance), PanelRigMap.BandAdvance('%'));
            Assert.Equal(PanelRigMap.BandAdvance('W'), PanelRigMap.BandAdvance('%'));
            // Each glyph a whole pixel, up: 0.878 of 10 px is 9.
            Assert.Equal(9, PanelRigMap.TrackedWidth("W", 10, 0));
            Assert.Equal(9 + 1.4, PanelRigMap.TrackedWidth("W", 10, 0.14), 6);
            // The panel draws the band's words in SemiBold, and embeds that face.
            Assert.Contains("FontWeights.SemiBold, paint.InkHex, tracking", RigMethod("private static Border RigPainted("));
            Assert.Contains("\"Barlow-SemiBold.ttf\"", File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "PanelFonts.cs")));
        }

        [Fact]
        public void A_bands_words_fit_the_band_of_every_screen_they_are_drawn_on()
        {
            // "WHITE · LAST LAP" is 16 glyphs; tracked at 10 px it is wider than a portrait pit wall's band.
            Assert.Equal(PanelRigMap.TrackedWidth("WHITE", 10, 0.14) + PanelRigMap.TrackedWidth(" · LAST LAP", 10, 0.14), PanelRigMap.TrackedWidth("WHITE · LAST LAP", 10, 0.14), 6);
            // W 9, H 7, I 3, T 6, E 6, the dot 3, the spaces 2, L 6, A 7, S 6, P 6, and 1.4 after each of 16.
            Assert.Equal(9 + 7 + 3 + 6 + 6 + 2 + 3 + 2 + 6 + 7 + 6 + 6 + 2 + 6 + 7 + 6 + 16 * 1.4, PanelRigMap.TrackedWidth("WHITE · LAST LAP", 10, 0.14), 6);

            Assert.Equal(0, PanelRigMap.TrackedWidth(null, 10, 0.14));
            Assert.Equal(75 - 10, PanelRigMap.ScreenInner(75));
            Assert.Equal("WHITE · LAST LAP", PanelRigMap.BandWords("WHITE · LAST LAP", 290));
            Assert.Equal("WHITE", PanelRigMap.BandWords("WHITE · LAST LAP", 65));
            Assert.Equal(PanelRigMap.BlockName(PanelEmulation.White), PanelRigMap.BandWords("WHITE · LAST LAP", 65));

            // Every screen's tile the rig can draw: each face size, both pit walls, both phones.
            var widths = new List<double>();
            foreach (var size in Contract.FaceSizes)
            {
                double w, h;
                PanelRigMap.FaceTileSize(size.Width, size.Height, out w, out h);
                widths.Add(w);
            }
            foreach (var screen in new[] { new[] { 1920, 1080 }, new[] { 1080, 1920 }, new[] { 850, 480 }, new[] { 480, 850 } })
            {
                double w, h;
                PanelRigMap.SecondScreenTileSize(false, screen[0], screen[1], out w, out h);
                widths.Add(w);
            }
            Assert.Contains(75.0, widths);
            foreach (var width in widths)
            {
                var inner = PanelRigMap.ScreenInner(width);
                foreach (var scenario in PanelEmulation.Scenarios().Select(s => s.Id))
                {
                    var faces = new[] { PanelRigMap.FlagFormatBand, PanelRigMap.FlagFormatFull };
                    var drawn = faces.SelectMany(f => new[] { PanelRigMap.FaceBandFor(scenario, f), PanelRigMap.FaceBlockFor(scenario, f), PanelRigMap.PitWallBandFor(scenario, f), PanelRigMap.PitWallBlockFor(scenario, f) });
                    foreach (var band in drawn)
                    {
                        var words = PanelRigMap.BandPaint(band, inner).Words;
                        if (words == null) continue;
                        Assert.True(PanelRigMap.TrackedWidth(words, PanelRigMap.BandTextSize, PanelRigMap.BandTracking) <= inner, width + " " + scenario + ": " + words);
                    }
                }
            }
        }

        [Fact]
        public void A_tile_that_wears_the_warning_dot_says_so_in_words()
        {
            var tile = PanelRigMap.Tiles(Rig()).Single(t => t.Id == "led:LedDashBrow");
            Assert.Equal("Dash brow", PanelRigMap.TileLabel(tile, false));
            // The sidebar's words for its dot, in the form the rail adds them.
            Assert.Equal("Dash brow · Needs attention", PanelRigMap.TileLabel(tile, true));
            Assert.Equal("Dash brow · " + PanelNav.WarnTooltip, PanelRigMap.TileLabel(tile, true));
            Assert.Equal(string.Empty, PanelRigMap.TileLabel(null, false));
            // A tooltip only where the tile says less than its label: the bare name, drawn whole, says it all
            // (voice.md: where the control already says it, say nothing).
            Assert.Null(PanelRigMap.TileTooltip(tile, false, false));
            Assert.Equal("Dash brow · Needs attention", PanelRigMap.TileTooltip(tile, true, false));
            Assert.Equal("Dash brow", PanelRigMap.TileTooltip(tile, false, true));
            Assert.Equal("Dash brow · Needs attention", PanelRigMap.TileTooltip(tile, true, true));
            var thumb = RigMethod("private RigTileView BuildRigTile(");
            InOrder(thumb,
                "name.MaxWidth = Math.Max(0, tile.Width - (warns ? PanelRigMap.WarnDot + PanelRigMap.WarnGap : 0));",
                "ToolTip = PanelRigMap.TileTooltip(tile, warns, RigNameTrimmed(tile.Name, name.MaxWidth)),",
                "AutomationProperties.SetName(thumb, PanelRigMap.TileLabel(tile, warns));");
            // Trimmed is measured as the line draws the name: its size and weight, unbounded.
            InOrder(RigMethod("private static bool RigNameTrimmed("), "Ui.Text(text ?? string.Empty, PanelRigMap.NameSize, FontWeights.Medium,", "probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));", "return probe.DesiredSize.Width > room;");
            Assert.Contains("var name = Ui.Text(tile.Name, PanelRigMap.NameSize, FontWeights.Medium, Theme.TextSecondary);", thumb);
        }

        [Fact]
        public void A_pit_wall_shows_the_page_it_is_on_with_each_zone_named_for_its_page()
        {
            var wall = Screen(Contract.KindPitWall, "PitWall", "Pit wall", 1920, 1080);
            var cells = PanelRigMap.PitWallCells(wall);
            // The Race page is the artboard's Leaderboard, Fuel and Tyres.
            Assert.Equal(new[] { "Leaderboard", "Fuel", "Tyres" }, cells.Select(c => c.Text));
            Assert.Equal(0, cells[0].X);
            Assert.Equal(0, cells[0].Y);
            Assert.Equal(1, cells[0].Height);
            Assert.True(cells[1].X > cells[0].X + cells[0].Width);
            Assert.True(cells[2].Y > cells[1].Y);
            Assert.All(cells, c => Assert.True(c.X + c.Width <= 1 && c.Y + c.Height <= 1));

            wall.SetZonePage("RaceA", 5);
            Assert.Equal("Leaderboard", PanelRigMap.PitWallCells(wall)[1].Text);
            wall.PitWallPage = 1;
            // The Tower page's tower is the same table as the Race page's board (pitwall.ts' race.board and
            // tower.board), so it is named for what it shows, as every zone on the tile is.
            Assert.Equal(new[] { PanelRigMap.BoardLabel, ZonePages.Wide[Contract.PitWallZoneSlotByKey("TowerWide").Fallback].Name, "Relative", "Opponents" }, PanelRigMap.PitWallCells(wall).Select(c => c.Text));
            Assert.False(PanelRigMap.PitWallPortrait(wall));

            // Every page the settings name is one the Screens page draws, by its title: the two lists are
            // typed apart, so a rename fails here rather than drawing a pit wall with no panels.
            for (var i = 0; i < Contract.PitWallPageNames.Length; i++)
            {
                var page = PanelRigMap.PitWallPlanPage(i);
                Assert.NotNull(page);
                Assert.Equal(Contract.PitWallPageNames[i], page.Title);
                wall.PitWallPage = i;
                var drawn = PanelRigMap.PitWallCells(wall);
                Assert.Equal(page.Panels.Count, drawn.Count);
                Assert.All(drawn, c => Assert.False(string.IsNullOrEmpty(c.Text), Contract.PitWallPageNames[i]));
            }
            Assert.Null(PanelRigMap.PitWallPlanPage(-1));
            Assert.Null(PanelRigMap.PitWallPlanPage(Contract.PitWallPageNames.Length));
        }

        [Fact]
        public void A_pit_wall_tile_follows_the_quick_glance_on_the_clock()
        {
            // The glance moves a zone's page on the press and back on the release, with no save and no
            // rebuild; PitWallState changes with it, so the page's clock repaints the tile both ways.
            var settings = Rig();
            var wall = settings.ScreenByNamespace("PitWall");
            wall.PitWallPage = 1;
            var tile = PanelRigMap.Tiles(settings).Single(t => t.Kind == RigTileKind.PitWall);
            var before = PanelRigMap.PitWallState(wall);
            Assert.Equal(before, PanelRigMap.LiveState(settings, tile));
            var named = PanelRigMap.PitWallCells(wall).Select(c => c.Text).ToList();

            var key = Contract.PitWallPageNames[1] + "B";
            Assert.NotNull(Contract.PitWallZoneSlotByKey(key));
            var was = wall.ZonePage(key);
            wall.SetZonePage(key, was == 0 ? 1 : 0);
            var glanced = PanelRigMap.PitWallState(wall);
            Assert.NotEqual(before, glanced);
            Assert.NotEqual(named, PanelRigMap.PitWallCells(wall).Select(c => c.Text).ToList());
            wall.SetZonePage(key, was);
            Assert.Equal(before, PanelRigMap.PitWallState(wall));

            // The page it is on is part of it, and so is a portrait pit wall's own page.
            wall.PitWallPage = 0;
            Assert.NotEqual(before, PanelRigMap.PitWallState(wall));
            var portrait = Screen(Contract.KindPitWall, "PitWallPortrait", "Pit wall portrait", 1080, 1920);
            var standing = PanelRigMap.PitWallState(portrait);
            Assert.StartsWith(PanelRigMap.PortraitPage + ":", standing);
            portrait.SetZonePage(PanelRigMap.PortraitPage + "A", portrait.ZonePage(PanelRigMap.PortraitPage + "A") == 0 ? 1 : 0);
            Assert.NotEqual(standing, PanelRigMap.PitWallState(portrait));

            Assert.Equal(string.Empty, PanelRigMap.PitWallState(null));
            // A face is watched by the pages its zones are on, which a wheel button moves.
            var main = settings.ScreenByNamespace("MainDash");
            main.Face = new FaceSettings();
            main.Face.Normalise();
            var face = PanelRigMap.Tiles(settings).Single(t => t.Id == "MainDash");
            var paged = PanelRigMap.LiveState(settings, face);
            Assert.NotNull(paged);
            Assert.Equal(PanelRigMap.FaceState(main), paged);
            main.Face.Zones[0] = main.Face.Zones[0] == 2 ? 3 : 2;
            Assert.NotEqual(paged, PanelRigMap.LiveState(settings, face));
            Assert.Equal(PanelRigMap.FaceState(main), PanelRigMap.LiveState(settings, face));

            // A tile that draws nothing a glance moves is not watched.
            Assert.Null(PanelRigMap.LiveState(settings, Tile(RigTileKind.Strip, "strip")));
            Assert.Null(PanelRigMap.LiveState(settings, Tile(RigTileKind.Matrix, "matrix")));
            Assert.Null(PanelRigMap.LiveState(settings, PanelRigMap.Tiles(settings).Single(t => t.Kind == RigTileKind.Round)));
            Assert.Null(PanelRigMap.LiveState(settings, PanelRigMap.Tiles(settings).Single(t => t.Kind == RigTileKind.Companion)));
        }

        [Fact]
        public void A_portrait_pit_wall_shows_its_own_page_rather_than_a_landscape_one()
        {
            var wall = Screen(Contract.KindPitWall, "PitWallPortrait", "Pit wall portrait", 1080, 1920);
            // Its page is not one of the landscape three, whatever PitWallPage says.
            wall.PitWallPage = 1;
            Assert.True(PanelRigMap.PitWallPortrait(wall));
            // "Portrait" is the settings' key for its zones, and never drawn.
            Assert.Contains(Contract.PitWallZoneSlots, s => s.Page == PanelRigMap.PortraitPage && !s.Landscape);

            var cells = PanelRigMap.PitWallCells(wall);
            var standard = ZonePages.Standard;
            Assert.Equal(new[]
            {
                "Leaderboard",
                standard[Contract.PitWallZoneSlotByKey("PortraitA").Fallback].Name,
                standard[Contract.PitWallZoneSlotByKey("PortraitB").Fallback].Name,
                standard[Contract.PitWallZoneSlotByKey("PortraitC").Fallback].Name,
                standard[Contract.PitWallZoneSlotByKey("PortraitD").Fallback].Name,
            }, cells.Select(c => c.Text));
            // The board across the top, then A and B side by side, C and D under them.
            Assert.Equal(new[] { 0.0, 0, 1 }, new[] { cells[0].X, cells[0].Y, cells[0].Width });
            Assert.Equal(0.43, cells[0].Height);
            Assert.Equal(0.49, cells[1].Y);
            Assert.Equal(0.49, cells[2].Y);
            Assert.Equal(0.49, cells[1].Width, 9);
            Assert.Equal(0.51, cells[2].X, 9);
            Assert.Equal(0.245, cells[1].Height, 9);
            Assert.Equal(0.755, cells[3].Y, 9);
            Assert.True(cells[1].Y > cells[0].Y + cells[0].Height);
            Assert.Equal(cells[1].Y, cells[2].Y);
            Assert.True(cells[2].X > cells[1].X + cells[1].Width);
            Assert.Equal(cells[1].X, cells[3].X);
            Assert.Equal(cells[2].X, cells[4].X);
            Assert.True(cells[3].Y > cells[1].Y + cells[1].Height);
            Assert.All(cells, c => Assert.True(c.X + c.Width <= 1 + 1e-9 && c.Y + c.Height <= 1 + 1e-9));

            // Each zone named for the page chosen for it.
            wall.SetZonePage("PortraitC", 5);
            Assert.Equal(standard[5].Name, PanelRigMap.PitWallCells(wall)[3].Text);
        }

        [Fact]
        public void A_strip_and_a_matrix_are_painted_with_their_own_settings()
        {
            var settings = Rig();
            var wheel = settings.LedBarByNamespace("LedWheelRim");
            wheel.SpotterWhole = true;
            wheel.SetEffect("flag.yellow", false);
            var options = PanelRigMap.StripOptionsFor(wheel);
            Assert.True(options.SpotterWhole);
            Assert.False(options.Draws("flag.yellow"));
            Assert.Equal(3, PanelRigMap.StripEnds(wheel));
            Assert.Equal(9, PanelRigMap.StripCentre(wheel));
            var brow = settings.LedBarByNamespace("LedDashBrow");
            Assert.Equal(0, PanelRigMap.StripEnds(brow));
            Assert.Equal(15, PanelRigMap.StripCentre(brow));
            Assert.Equal(3, PanelRigMap.StripEnds(null));
            Assert.Equal(9, PanelRigMap.StripCentre(null));

            settings.FlagBoxSide[0] = "left";
            settings.SetMatrixRest(1, "dark");
            settings.FlagBoxSpotter[0] = false;
            var matrix = PanelRigMap.MatrixOptionsFor(settings, 1);
            Assert.Equal("left", matrix.Side);
            Assert.Equal("dark", matrix.Rest);
            Assert.False(matrix.Spotter);
            Assert.Equal(settings.MatrixGearBands(1), matrix.Bands);
            Assert.Equal(settings.MatrixGearCarLadder(1), matrix.CarLadder);
            Assert.True(matrix.Flags);
            Assert.True(matrix.Pit);
            Assert.True(matrix.Warnings);

            // Each family its own switch, off where the panel's is: none is read from another.
            settings.FlagBoxPit[0] = false;
            settings.FlagBoxWarnings[0] = true;
            settings.FlagBoxFlags[0] = false;
            matrix = PanelRigMap.MatrixOptionsFor(settings, 1);
            Assert.False(matrix.Pit);
            Assert.True(matrix.Warnings);
            Assert.False(matrix.Flags);
            settings.FlagBoxPit[0] = true;
            settings.FlagBoxWarnings[0] = false;
            matrix = PanelRigMap.MatrixOptionsFor(settings, 1);
            Assert.True(matrix.Pit);
            Assert.False(matrix.Warnings);
            // The second slot is its own.
            Assert.True(PanelRigMap.MatrixOptionsFor(settings, 2).Flags);
        }

        /// <summary>A strip's tile carries its Centre display into the emulation, which draws a centre that is not
        /// the revs as its stand-in (PanelEmulationTests holds the stand-ins); the page no longer takes the rev
        /// ladder out of a frame after the fact (#523).</summary>
        [Fact]
        public void A_strip_whose_centre_is_not_the_revs_draws_no_rev_lights_there()
        {
            var settings = Rig();
            var wheel = settings.LedBarByNamespace("LedWheelRim");
            Assert.Equal(Contract.DefaultLedCentre, PanelRigMap.StripCentreDisplay(settings, wheel));
            Assert.Equal(Contract.DefaultLedCentre, PanelRigMap.StripCentreDisplay(null, wheel));
            Assert.True(PanelRigMap.StripOptionsFor(settings, wheel).CentreShowsRevs);
            Assert.Equal(PanelEmulation.StripFrame(3, 9, PanelEmulation.Shift), PanelEmulation.StripFrame(3, 9, PanelEmulation.Shift, PanelRigMap.StripOptionsFor(settings, wheel)));
            wheel.Centre = "fuel";
            Assert.Equal("fuel", PanelRigMap.StripCentreDisplay(settings, wheel));
            var options = PanelRigMap.StripOptionsFor(settings, wheel);
            Assert.Equal("fuel", options.Centre);
            Assert.False(options.CentreShowsRevs);
            Assert.Equal(PanelRigMap.StripOptionsFor(wheel).SpotterWhole, options.SpotterWhole);
            Assert.Equal(PanelRigMap.StripOptionsFor(wheel).EffectsOff, options.EffectsOff);
            foreach (var revs in new[] { PanelEmulation.Idle, PanelEmulation.Mid, PanelEmulation.Shift })
            {
                Assert.All(PanelEmulation.StripFrame(3, 9, revs, options)[1], led => Assert.Null(led));
            }
            Assert.DoesNotContain("StripPicture", RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "PanelRigMap.cs")));
        }

        [Fact]
        public void A_flag_is_drawn_on_a_strip_by_an_effect_the_strip_has()
        {
            // Each by its own id; the contract's rows name every one but red, which the strip draws under the
            // flags' one switch all the same.
            Assert.Equal(new[] { "flag.green", "flag.yellow", "flag.blue", "flag.white", "flag.black", "flag.chequered", "flag.red" },
                PanelEmulation.Scenarios().Select(s => s.Id).Where(PanelEmulation.IsFlag).Select(PanelRigMap.StripFlagEffect));
            var ids = Contract.LedEffectIds().ToList();
            Assert.All(PanelEmulation.Scenarios().Select(s => s.Id).Where(id => PanelEmulation.IsFlag(id) && id != PanelEmulation.Red), id => Assert.Contains(PanelRigMap.StripFlagEffect(id), ids));
        }

        [Fact]
        public void A_matrix_on_critical_flags_only_does_not_show_the_flags_that_are_news()
        {
            var settings = Rig();
            Assert.True(PanelRigMap.MatrixOptionsFor(settings, 1, PanelEmulation.Green).Flags);
            settings.FlagBoxMatrixCriticalOnly[0] = true;
            // flags.ts's critical four still show; green, white and the chequer do not.
            foreach (var id in new[] { PanelEmulation.Yellow, PanelEmulation.Blue, PanelEmulation.Black, PanelEmulation.Red })
            {
                Assert.True(PanelRigMap.CriticalFlag(id), id);
                Assert.True(PanelRigMap.MatrixOptionsFor(settings, 1, id).Flags, id);
            }
            foreach (var id in new[] { PanelEmulation.Green, PanelEmulation.White, PanelEmulation.Chequer })
            {
                Assert.False(PanelRigMap.CriticalFlag(id), id);
                Assert.False(PanelRigMap.MatrixOptionsFor(settings, 1, id).Flags, id);
                Assert.NotEqual(id == PanelEmulation.Chequer ? "chequered" : id, PanelEmulation.GlyphFor(id, PanelRigMap.MatrixOptionsFor(settings, 1, id)));
            }
            // The other panel, and a scenario that is not a flag, are untouched.
            Assert.True(PanelRigMap.MatrixOptionsFor(settings, 2, PanelEmulation.Green).Flags);
            Assert.True(PanelRigMap.MatrixOptionsFor(settings, 1, PanelEmulation.Mid).Flags);
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorCanvas = rig.canvas",
                "AnchorScenarios = rig.scenarios",
            }, AnchorTable.Of(typeof(PanelRigMap)));
        }
    }
}
