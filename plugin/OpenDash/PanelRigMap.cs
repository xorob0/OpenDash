// PanelRigMap.cs: the Rig page's words -- its title, what search finds on it, and the layout its tiles take
// before anybody has dragged them.
//
// Named for the map rather than "PanelRig", which was the old Rig tab's and is now PanelScreens. The Rig
// page draws every screen, strip and matrix as a tile on a dotted canvas and paints a scenario on all of
// them at once (PanelEmulation). The Rig page agent owns this file and adds drag and the saved positions.
// Pure: no WPF.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>What kind of thing a tile draws.</summary>
    public enum RigTileKind
    {
        Face,
        PitWall,
        Companion,
        Round,
        Strip,
        Matrix,
    }

    /// <summary>One tile: what it is, what it is called, and where it sits on the canvas.</summary>
    public sealed class RigTile
    {
        public RigTile(RigTileKind kind, string id, string name, double x, double y, double width, double height)
        {
            Kind = kind;
            Id = id;
            Name = name;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public RigTileKind Kind { get; private set; }

        /// <summary>The screen's or strip's namespace, or the matrix slot as text.</summary>
        public string Id { get; private set; }

        public string Name { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }

        /// <summary>The picture's size, without the name above it.</summary>
        public double Width { get; private set; }
        public double Height { get; private set; }
    }

    public static class PanelRigMap
    {
        public const string Title = "Rig";

        /// <summary>The canvas's height, which the artboard draws at 580.</summary>
        public const double CanvasHeight = 580;

        /// <summary>The canvas's dotted grid.</summary>
        public const double GridStep = 20;

        /// <summary>The line in the canvas's corner.</summary>
        public const string CanvasHint = "Drag to arrange like your rig";

        public const string ResetLayout = "Reset layout";

        /// <summary>The canvas of a rig with nothing on it, beside a press that goes to Screens.</summary>
        public const string Empty = "Nothing on the rig yet.";

        /// <summary>The greyed switch in the header (#506), by its registry title; search finds it through
        /// PanelSoon rather than an entry of this page's own.</summary>
        public const string RealHardwareTitle = "Real hardware";

        /// <summary>The name over a tile, and the gap under it.</summary>
        public const double NameHeight = 16;
        public const double NameGap = 6;

        /// <summary>A face tile's width, which the artboard draws the main dash at, scaled to the screen's aspect.</summary>
        public const double FaceWidth = 300;
        public const double PitWallWidth = 240;
        public const double CompanionWidth = 76;
        public const double CompanionHeight = 135;
        public const double RoundSize = 110;

        public const string AnchorCanvas = "rig.canvas";
        public const string AnchorScenarios = "rig.scenarios";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(Title, PanelPage.Rig, AnchorCanvas, "rig layout", "map", "arrange", "tiles"),
            new PanelSearch.Entry("Flags", PanelPage.Rig, AnchorScenarios, "emulate", "test", "yellow", "blue", "chequered"),
            new PanelSearch.Entry("Spotter", PanelPage.Rig, AnchorScenarios, "emulate", "car left", "car right"),
            new PanelSearch.Entry("Pit lane", PanelPage.Rig, AnchorScenarios, "emulate", "limiter", "speeding"),
            new PanelSearch.Entry("Warnings", PanelPage.Rig, AnchorScenarios, "emulate", "fuel", "oil", "water"),
            new PanelSearch.Entry("Revs", PanelPage.Rig, AnchorScenarios, "emulate", "shift point", "rpm"),
        };

        /// <summary>The empty edge round a laid-out canvas, and the gap between two tiles across and down.</summary>
        public const double LayoutMargin = 24;
        public const double LayoutGap = 28;

        /// <summary>
        /// Where the tiles go before anybody has arranged them: by kind, in rows across the canvas, each row
        /// as tall as its tallest tile.
        /// </summary>
        /// <remarks>
        /// Screens first, because a rig is mostly screens, then the strips, then the matrices. A tile never
        /// starts past the canvas's width; a row that would is wrapped. Not the artboard's nine positions,
        /// which are one imagined rig: a rig with two faces and no strips has to come out tidy too.
        /// </remarks>
        public static IList<RigTile> AutoLayout(IEnumerable<RigTile> tiles, double canvasWidth)
        {
            const double margin = LayoutMargin;
            const double gap = LayoutGap;
            var placed = new List<RigTile>();
            if (tiles == null) return placed;
            // Stable, so tiles of one kind keep the rig's order: List.Sort is not, and laid a rig of pit wall
            // A, pit wall B and a rim out as the rim, B, A.
            var ordered = tiles.Where(tile => tile != null).Select((tile, index) => new { tile, index })
                .OrderBy(entry => Order(entry.tile.Kind)).ThenBy(entry => entry.index)
                .Select(entry => entry.tile).ToList();

            var x = margin;
            var y = margin;
            var rowHeight = 0.0;
            var lastGroup = -1;
            foreach (var tile in ordered)
            {
                var group = Order(tile.Kind) < 4 ? 0 : Order(tile.Kind);
                var tall = tile.Height + NameHeight + NameGap;
                var newRow = (lastGroup >= 0 && group != lastGroup) || (x > margin && x + tile.Width > canvasWidth - margin);
                if (newRow)
                {
                    x = margin;
                    y += rowHeight + gap;
                    rowHeight = 0;
                }
                placed.Add(new RigTile(tile.Kind, tile.Id, tile.Name, x, y, tile.Width, tile.Height));
                x += tile.Width + gap;
                rowHeight = Math.Max(rowHeight, tall);
                lastGroup = group;
            }
            return placed;
        }

        private static int Order(RigTileKind kind)
        {
            switch (kind)
            {
                case RigTileKind.Face: return 0;
                case RigTileKind.Round: return 1;
                case RigTileKind.PitWall: return 2;
                case RigTileKind.Companion: return 3;
                case RigTileKind.Strip: return 4;
                default: return 5;
            }
        }
    }
}
