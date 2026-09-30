// PanelRigMapTests.cs: the Rig page's words and sizes, the tile each device becomes, where the tiles go before
// and after a driver drags them, and what each tile shows under a scenario.
using System;
using System.Collections.Generic;
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
            Assert.Equal(9, PanelRigMap.RevRowHeight);
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
        }

        [Fact]
        public void Search_finds_the_header_and_every_group_of_chips_on_the_rig_page()
        {
            Assert.Equal(new[] { "Rig", "Night mode", "Reset layout", "Flags", "Spotter", "Pit lane", "Warnings", "Revs" }, PanelRigMap.Search.Select(e => e.Label));
            Assert.All(PanelRigMap.Search, e => Assert.Equal(PanelPage.Rig, e.Route.Page));
            Assert.Equal(PanelRigMap.AnchorCanvas, PanelRigMap.Search[0].Route.Anchor);
            Assert.All(PanelRigMap.Search.Skip(3), e => Assert.Equal(PanelRigMap.AnchorScenarios, e.Route.Anchor));
            Assert.Equal(PanelSettings.NightModeTitle, PanelRigMap.Search[1].Label);
            Assert.DoesNotContain(PanelRigMap.Search, e => e.Label == PanelRigMap.RealHardwareTitle);
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

        [Fact]
        public void A_tile_dropped_against_an_edge_is_drawn_again_where_it_was_dropped()
        {
            var settings = Rig();
            const double width = 1083;
            var plan = PanelRigMap.Plan(settings, width);
            var rim = plan.Tiles.Single(t => t.Id == "Rim");
            // Pushed past the right edge and the foot.
            var x = PanelRigMap.DropPosition(width, PanelRigMap.FootprintWidth(rim), width);
            var y = PanelRigMap.DropPosition(plan.Height, PanelRigMap.FootprintHeight(rim), plan.Height);
            Assert.True(x + PanelRigMap.FootprintWidth(rim) <= width);
            Assert.True(y + PanelRigMap.FootprintHeight(rim) <= plan.Height);
            PanelRigMap.SavePosition(settings, rim, x, y);
            Assert.Equal((int)x, settings.ScreenByNamespace("Rim").LayoutX);
            Assert.Equal((int)y, settings.ScreenByNamespace("Rim").LayoutY);
            var again = PanelRigMap.Plan(settings, width).Tiles.Single(t => t.Id == "Rim");
            Assert.Equal(new[] { x, y }, new[] { again.X, again.Y });
        }

        [Fact]
        public void A_dropped_tile_is_kept_in_its_own_settings_and_moves_no_other()
        {
            var settings = Rig();
            var before = PanelRigMap.Plan(settings, 1100).Tiles.ToDictionary(t => t.Id);
            var tiles = PanelRigMap.Tiles(settings).ToDictionary(t => t.Id);

            PanelRigMap.SavePosition(settings, tiles["Rim"], 433, 51);
            PanelRigMap.SavePosition(settings, tiles["led:LedWheelRim"], 118, 302);
            PanelRigMap.SavePosition(settings, tiles["matrix:2"], -30, 71);
            Assert.Equal(440, settings.ScreenByNamespace("Rim").LayoutX);
            Assert.Equal(60, settings.ScreenByNamespace("Rim").LayoutY);
            Assert.Equal(120, settings.LedBarByNamespace("LedWheelRim").LayoutX);
            Assert.Equal(300, settings.LedBarByNamespace("LedWheelRim").LayoutY);
            Assert.Equal(0, settings.MatrixLayoutX[1]);
            Assert.Equal(80, settings.MatrixLayoutY[1]);
            Assert.Null(settings.MatrixLayoutX[0]);

            int x, y;
            Assert.True(PanelRigMap.TrySaved(settings, tiles["Rim"], out x, out y));
            Assert.Equal(new[] { 440, 60 }, new[] { x, y });
            Assert.False(PanelRigMap.TrySaved(settings, tiles["MainDash"], out x, out y));

            var after = PanelRigMap.Plan(settings, 1100).Tiles.ToDictionary(t => t.Id);
            Assert.Equal(440, after["Rim"].X);
            Assert.Equal(60, after["Rim"].Y);
            Assert.Equal(0, after["matrix:2"].X);
            foreach (var id in new[] { "MainDash", "PitWall", "Companion", "Slots480x480", "led:LedDashBrow", "matrix:1" })
            {
                Assert.Equal(before[id].X, after[id].X);
                Assert.Equal(before[id].Y, after[id].Y);
            }

            // A canvas narrowed since holds a saved tile inside it without forgetting where it was put.
            var narrow = PanelRigMap.Plan(settings, 500).Tiles.ToDictionary(t => t.Id);
            Assert.Equal(500 - 300, narrow["Rim"].X);
            Assert.Equal(440, settings.ScreenByNamespace("Rim").LayoutX);

            // And a canvas shorter than the place a tile was kept at holds it on the foot, the place kept.
            PanelRigMap.SavePosition(settings, tiles["Rim"], 440, 560);
            var low = PanelRigMap.Plan(settings, 1100);
            Assert.Equal(PanelRigMap.CanvasHeight, low.Height);
            Assert.Equal(580 - PanelRigMap.FootprintHeight(tiles["Rim"]), low.Tiles.Single(t => t.Id == "Rim").Y);
            Assert.Equal(560, settings.ScreenByNamespace("Rim").LayoutY);
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
            foreach (var tile in tiles) PanelRigMap.SavePosition(settings, tile, 100, 100);
            Assert.All(tiles, tile => { int x, y; Assert.True(PanelRigMap.TrySaved(settings, tile, out x, out y)); });
            Assert.True(PanelRigMap.ClearLayout(settings));
            Assert.All(tiles, tile => { int x, y; Assert.False(PanelRigMap.TrySaved(settings, tile, out x, out y)); });
            Assert.All(settings.RigScreens(), s => Assert.Null(s.LayoutX));
            Assert.All(settings.LedBarList(), b => Assert.Null(b.LayoutY));
            Assert.All(settings.MatrixLayoutX, v => Assert.Null(v));
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
            Assert.Equal("Fuel", PanelRigMap.FaceBandIdle(screen));

            // The page a zone is on now, not the one it opens on.
            screen.Face = new FaceSettings();
            screen.Face.Normalise();
            screen.Face.Zones[0] = 2;
            screen.Face.Zones[3] = 3;
            zones = PanelRigMap.FaceZones(screen);
            Assert.Equal("Speed", zones[1].Text);
            Assert.False(zones[1].Gear);
            Assert.Equal("Tyres", PanelRigMap.FaceBandIdle(screen));

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
        public void A_flag_fills_the_phone_and_nothing_else_does()
        {
            Assert.Equal(Theme.FlagYellow, PanelRigMap.CompanionBand(PanelEmulation.Yellow).FillHex);
            Assert.Equal("YELLOW", PanelRigMap.CompanionBand(PanelEmulation.Yellow).Text);
            Assert.True(PanelRigMap.CompanionBand(PanelEmulation.Black).Outlined);
            Assert.True(PanelRigMap.CompanionBand(PanelEmulation.Chequer).Chequer);
            Assert.False(PanelRigMap.CompanionBand(PanelEmulation.Limiter).Alert);
            Assert.False(PanelRigMap.CompanionBand(PanelEmulation.CarLeft).Alert);
            var phone = Screen(Contract.KindCompanion, "Companion", "Phone", 850, 480);
            Assert.Equal(Modules.All[Contract.DefaultCompanionStart].Name, PanelRigMap.CompanionIdle(phone));
            phone.CompanionStart = 3;
            Assert.Equal(Modules.All[3].Name, PanelRigMap.CompanionIdle(phone));
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
            Assert.Equal("Race", PanelRigMap.PitWallBandIdle(wall));

            wall.SetZonePage("RaceA", 5);
            Assert.Equal("Leaderboard", PanelRigMap.PitWallCells(wall)[1].Text);
            wall.PitWallPage = 1;
            Assert.Equal(new[] { "Tower", ZonePages.Wide[Contract.PitWallZoneSlotByKey("TowerWide").Fallback].Name, "Relative", "Opponents" }, PanelRigMap.PitWallCells(wall).Select(c => c.Text));
            Assert.Equal("Tower", PanelRigMap.PitWallBandIdle(wall));
            Assert.False(PanelRigMap.PitWallPortrait(wall));
        }

        [Fact]
        public void A_portrait_pit_wall_shows_its_own_page_rather_than_a_landscape_one()
        {
            var wall = Screen(Contract.KindPitWall, "PitWallPortrait", "Pit wall portrait", 1080, 1920);
            // Its page is not one of the landscape three, whatever PitWallPage says.
            wall.PitWallPage = 1;
            Assert.True(PanelRigMap.PitWallPortrait(wall));
            Assert.Equal("Portrait", PanelRigMap.PitWallBandIdle(wall));
            Assert.DoesNotContain(PanelRigMap.PitWallBandIdle(wall), Contract.PitWallPageNames);
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
            Assert.Equal(settings.MatrixFlags(1), matrix.Flags);
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
