// PanelRigMapTests.cs: the Rig page's words and sizes, and how AutoLayout places a rig nobody has arranged.
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

        [Fact]
        public void The_rig_page_is_drawn_at_the_artboards_sizes()
        {
            Assert.Equal("Rig", PanelRigMap.Title);
            Assert.Equal("Drag to arrange like your rig", PanelRigMap.CanvasHint);
            Assert.Equal("Reset layout", PanelRigMap.ResetLayout);
            Assert.Equal("Real hardware", PanelRigMap.RealHardwareTitle);
            Assert.NotNull(PanelSoon.Find(PanelRigMap.RealHardwareTitle));
            Assert.Equal(580, PanelRigMap.CanvasHeight);
            Assert.Equal(20, PanelRigMap.GridStep);
            Assert.Equal(300, PanelRigMap.FaceWidth);
            Assert.Equal(240, PanelRigMap.PitWallWidth);
            Assert.Equal(76, PanelRigMap.CompanionWidth);
            Assert.Equal(135, PanelRigMap.CompanionHeight);
            Assert.Equal(110, PanelRigMap.RoundSize);
            Assert.Equal(16, PanelRigMap.NameHeight);
            Assert.Equal(6, PanelRigMap.NameGap);
            Assert.Equal(24, PanelRigMap.LayoutMargin);
            Assert.Equal(28, PanelRigMap.LayoutGap);
        }

        [Fact]
        public void Screens_come_first_then_strips_then_matrices_each_on_rows_of_their_own()
        {
            var tiles = new[] { Tile(RigTileKind.Matrix, "1"), Tile(RigTileKind.Strip, "rim"), Tile(RigTileKind.Face, "main") };
            var placed = PanelRigMap.AutoLayout(tiles, 1000);
            Assert.Equal(new[] { "main", "rim", "1" }, placed.Select(t => t.Id));
            Assert.Equal(24, placed[0].X);
            Assert.Equal(24, placed[0].Y);
            // Each kind group starts a row: 24 + 50 + 16 + 6 + 28 below the one before.
            Assert.Equal(24 + 50 + 16 + 6 + 28, placed[1].Y);
            Assert.Equal(24, placed[1].X);
            Assert.Equal(placed[1].Y + 50 + 16 + 6 + 28, placed[2].Y);
        }

        [Fact]
        public void A_row_that_would_pass_the_canvas_wraps()
        {
            var tiles = Enumerable.Range(0, 4).Select(i => Tile(RigTileKind.Face, "f" + i, 300, 80)).ToArray();
            // 24 + 300 + 28 + 300 = 652 fits a 700 canvas less its 24 margin; a third would not.
            var placed = PanelRigMap.AutoLayout(tiles, 700);
            Assert.Equal(new double[] { 24, 352, 24, 352 }, placed.Select(t => t.X));
            Assert.Equal(placed[0].Y, placed[1].Y);
            Assert.Equal(placed[0].Y + 80 + 16 + 6 + 28, placed[2].Y);
            // A tile wider than the canvas still starts a row of its own rather than vanishing.
            Assert.Single(PanelRigMap.AutoLayout(new[] { Tile(RigTileKind.Face, "wide", 900, 80) }, 700));
        }

        [Fact]
        public void Tiles_of_one_kind_keep_the_rigs_order()
        {
            var rig = new[] { Tile(RigTileKind.PitWall, "Pit wall A"), Tile(RigTileKind.PitWall, "Pit wall B"), Tile(RigTileKind.Face, "Rim") };
            Assert.Equal(new[] { "Rim", "Pit wall A", "Pit wall B" }, PanelRigMap.AutoLayout(rig, 2000).Select(t => t.Id));
            // Twenty strips, which List.Sort's introsort put out of order.
            var strips = Enumerable.Range(0, 20).Select(i => Tile(RigTileKind.Strip, "s" + i, 10, 10)).ToList();
            Assert.Equal(strips.Select(t => t.Id), PanelRigMap.AutoLayout(strips, 5000).Select(t => t.Id));
            Assert.Empty(PanelRigMap.AutoLayout(null, 1000));
        }
    }
}
