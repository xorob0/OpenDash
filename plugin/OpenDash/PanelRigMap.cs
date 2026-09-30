// PanelRigMap.cs: the Rig page's words and decisions -- its title, what search finds on it, the tile every
// device on the rig becomes, where a tile sits before and after a driver has dragged it, and what each tile
// shows under a scenario.
//
// Named for the map rather than "PanelRig", which was the old Rig tab's and is now PanelScreens. The Rig
// page draws every screen, strip and matrix as a tile on a dotted canvas the driver arranges into the shape
// of the rig, and paints a scenario on all of them at once (PanelEmulation). Rig.dc.html is the artboard.
// SettingsControl.Rig.cs only draws what this file decides, so every size, word and rule here is held by
// PanelRigMapTests. Nothing here lights the real hardware: that is #506.
//
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

        /// <summary>Unique across the canvas: a screen's namespace, "led:" and a strip's namespace, or
        /// "matrix:" and a slot (<see cref="PanelRigMap.StripId"/>, <see cref="PanelRigMap.MatrixId"/>).</summary>
        public string Id { get; private set; }

        /// <summary>What the settings know the device by: the namespace of a screen or a strip, or the
        /// matrix slot as text, which is also what PanelAttention names an issue's subject by.</summary>
        public string Key
        {
            get
            {
                if (Id == null) return null;
                if (Kind == RigTileKind.Strip && Id.StartsWith(PanelRigMap.StripIdPrefix, StringComparison.Ordinal)) return Id.Substring(PanelRigMap.StripIdPrefix.Length);
                if (Kind == RigTileKind.Matrix && Id.StartsWith(PanelRigMap.MatrixIdPrefix, StringComparison.Ordinal)) return Id.Substring(PanelRigMap.MatrixIdPrefix.Length);
                return Id;
            }
        }

        public string Name { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }

        /// <summary>The picture's size, without the name above it.</summary>
        public double Width { get; private set; }
        public double Height { get; private set; }

        /// <summary>The same tile somewhere else.</summary>
        public RigTile At(double x, double y)
        {
            return new RigTile(Kind, Id, Name, x, y, Width, Height);
        }
    }

    /// <summary>Every tile of the rig where it is drawn, and how tall the canvas is to hold them.</summary>
    public sealed class RigPlan
    {
        public RigPlan(IList<RigTile> tiles, double height)
        {
            Tiles = tiles;
            Height = height;
        }

        public IList<RigTile> Tiles { get; private set; }

        /// <summary><see cref="PanelRigMap.CanvasHeight"/>, or more where the fallback's rows need it.</summary>
        public double Height { get; private set; }
    }

    /// <summary>One of a face's three body zones: its letter, what it shows, and its share of the body.</summary>
    public sealed class RigZone
    {
        public RigZone(string letter, string text, bool gear, double weight)
        {
            Letter = letter;
            Text = text;
            Gear = gear;
            Weight = weight;
        }

        public string Letter { get; private set; }

        /// <summary>The page's name, or the gear the tiles are drawn in (<see cref="PanelEmulation.Gear"/>).</summary>
        public string Text { get; private set; }

        /// <summary>Drawn as the big gear digit rather than a page's name.</summary>
        public bool Gear { get; private set; }

        /// <summary>The zone's share of the body along the axis the body lays its zones out on.</summary>
        public double Weight { get; private set; }
    }

    /// <summary>One panel of a pit wall's page, as fractions of the tile's body.</summary>
    public sealed class RigCell
    {
        public RigCell(string text, double x, double y, double width, double height)
        {
            Text = text;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string Text { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
    }

    public static class PanelRigMap
    {
        public const string Title = "Rig";

        /// <summary>The canvas's height, which the artboard draws at 580.</summary>
        public const double CanvasHeight = 580;

        /// <summary>The canvas's dotted grid, and the step a dropped tile snaps to. Ui.DotGrid reads it.</summary>
        public const double GridStep = 20;

        /// <summary>The line in the canvas's corner.</summary>
        public const string CanvasHint = "Drag to arrange like your rig";

        /// <summary>The room the hint takes at the canvas's foot, which the fallback's last row stays clear
        /// of: its <see cref="HintBottom"/> under it, its 16 px line, and 16 over it.</summary>
        public const double HintClear = 44;

        public const string ResetLayout = "Reset layout";

        /// <summary>The canvas of a rig with nothing on it, beside a press that goes to Screens.</summary>
        public const string Empty = "Nothing on the rig yet.";

        /// <summary>The greyed switch in the header (#506), by its registry title; search finds it through
        /// PanelSoon rather than an entry of this page's own. The artboard's "Light the real hardware" is an
        /// instruction, and a switch names the thing.</summary>
        public const string RealHardwareTitle = "Real hardware";

        // --- The header -----------------------------------------------------------------------------

        /// <summary>Between the title and its New tag, between the header's controls, and between a
        /// switch's words and the switch.</summary>
        public const double TitleTagGap = 12;
        public const double HeaderGap = 18;
        public const double HeaderLabelGap = 10;

        /// <summary>Under the title, where the header's controls are stacked below it.</summary>
        public const double HeaderStackGap = 12;

        /// <summary>The Reset layout press: the artboard's .btn is 30 high and 12 in, where the kit's
        /// Small is 28 and 10 and its Regular 34 and 14.</summary>
        public const double ResetButtonHeight = 30;
        public const double ResetButtonPaddingX = 12;

        // --- The canvas and its tiles ------------------------------------------------------------------

        /// <summary>The hint's place in the canvas's lower left.</summary>
        public const double HintLeft = 14;
        public const double HintBottom = 12;

        /// <summary>The name over a tile, the gap under it, and the warning dot after it.</summary>
        public const double NameHeight = 16;
        public const double NameGap = 6;
        public const double NameSize = 12;
        public const double WarnDot = 6;
        public const double WarnGap = 6;

        /// <summary>A face tile's width, at which the artboard draws a 1280 x 480 rim, scaled to the screen's
        /// own aspect; a face taller than <see cref="FaceMaxHeight"/> is drawn narrower instead.</summary>
        public const double FaceWidth = 300;
        public const double FaceMaxHeight = 180;

        /// <summary>The box a pit wall or a phone is fitted inside at its own aspect: the artboard's 1920 x 1080
        /// pit wall fills it, and its portrait phone is 135 high.</summary>
        public const double PitWallWidth = 240;
        public const double PitWallHeight = 135;

        /// <summary>A phone of no known size, drawn as the artboard's.</summary>
        public const double CompanionWidth = 76;
        public const double CompanionHeight = 135;
        public const double RoundSize = 110;

        /// <summary>A screen's frame: 4 in, 3 between its rows, corners of 3.</summary>
        public const double ScreenPadding = 4;
        public const double ScreenGap = 3;
        public const double ScreenRadius = 3;

        /// <summary>A face's rev strip: twenty segments, 9 high, 2 apart, on the base ground 1 down and 2 in.</summary>
        public const int RevSegments = 20;
        public const double RevRowHeight = 9;
        public const double RevGap = 2;
        public const double RevPadX = 2;
        public const double RevPadY = 1;

        /// <summary>A zone's page name, and the band's words: 10 px, the band's tracked.</summary>
        public const double ZoneTextSize = 10;
        public const double BandTextSize = 10;
        public const double BandTracking = 0.14;

        /// <summary>The band is 18 per cent of a face's height (24 on the artboard's 135, 20 on its 112),
        /// and never under 12.</summary>
        public const double BandRatio = 0.18;
        public const double BandMinHeight = 12;

        /// <summary>The black flag's outline on a band, and on a phone.</summary>
        public const double BandOutline = 2;
        public const double CompanionOutline = 3;

        /// <summary>The chequered flag's squares: the artboard's 10 px conic tile holds four.</summary>
        public const double ChequerSquare = 5;

        public const double PitWallBandHeight = 16;
        public const double PitWallCellPadding = 4;

        public const double CompanionRadius = 6;
        public const double CompanionTextSize = 9;
        public const double CompanionTracking = 0.12;

        /// <summary>A round face's ring, and the gear inside it.</summary>
        public const double RingThickness = 6;
        public const double RoundGearSize = 44;

        // --- The scenario chips ---------------------------------------------------------------------

        /// <summary>Between groups across and down, under a group's title, and between two chips.</summary>
        public const double GroupGapX = 32;
        public const double GroupGapY = 18;
        public const double GroupTitleGap = 8;
        public const double ChipGap = 6;

        // --- Anchors and search ---------------------------------------------------------------------

        public const string AnchorCanvas = "rig.canvas";
        public const string AnchorScenarios = "rig.scenarios";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(Title, PanelPage.Rig, AnchorCanvas, "rig layout", "map", "arrange", "tiles", "emulate"),
            new PanelSearch.Entry(PanelSettings.NightModeTitle, PanelPage.Rig, null, "dark", "dim"),
            new PanelSearch.Entry(ResetLayout, PanelPage.Rig, null, "arrange", "tiles", "rig layout"),
            new PanelSearch.Entry(PanelEmulation.FlagsGroup, PanelPage.Rig, AnchorScenarios, "emulate", "test", "yellow", "blue", "chequered"),
            new PanelSearch.Entry(PanelEmulation.SpotterGroup, PanelPage.Rig, AnchorScenarios, "emulate", "car left", "car right"),
            new PanelSearch.Entry(PanelEmulation.PitLaneGroup, PanelPage.Rig, AnchorScenarios, "emulate", "limiter", "speeding"),
            new PanelSearch.Entry(PanelEmulation.WarningsGroup, PanelPage.Rig, AnchorScenarios, "emulate", "fuel", "oil", "water"),
            new PanelSearch.Entry(PanelEmulation.RevsGroup, PanelPage.Rig, AnchorScenarios, "emulate", "shift point", "rpm"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = { PanelSoon.RealHardware };

        /// <summary>
        /// Search labels this page draws through something other than the constant, each with the text its
        /// sources draw it by, so the list is a record rather than a way round PanelSearchTests.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // The scenario chips' groups are drawn from the list the constants are built into.
            { PanelEmulation.FlagsGroup, "PanelEmulation.Groups" },
            { PanelEmulation.SpotterGroup, "PanelEmulation.Groups" },
            { PanelEmulation.PitLaneGroup, "PanelEmulation.Groups" },
            { PanelEmulation.WarningsGroup, "PanelEmulation.Groups" },
            { PanelEmulation.RevsGroup, "PanelEmulation.Groups" },
        };

        // --- The tiles --------------------------------------------------------------------------------

        public const string StripIdPrefix = "led:";
        public const string MatrixIdPrefix = "matrix:";

        public static string StripId(string ns) { return StripIdPrefix + ns; }

        public static string MatrixId(int slot) { return MatrixIdPrefix + slot.ToString(System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Every device on the rig as a tile at the size its picture is drawn, in the rig's order:
        /// the screens, the strips, then the matrices. Positions are zero; <see cref="Arrange"/> places them.</summary>
        public static IList<RigTile> Tiles(OpenDashSettings settings)
        {
            var tiles = new List<RigTile>();
            if (settings == null) return tiles;
            foreach (var screen in settings.RigScreens())
            {
                if (screen == null) continue;
                var name = screen.Name ?? string.Empty;
                double w, h;
                if (screen.IsCompanion || screen.IsPitWall)
                {
                    SecondScreenTileSize(screen.IsCompanion, screen.Width, screen.Height, out w, out h);
                    tiles.Add(new RigTile(screen.IsCompanion ? RigTileKind.Companion : RigTileKind.PitWall, screen.Namespace, name, 0, 0, w, h));
                }
                else if (screen.IsSlots) tiles.Add(new RigTile(RigTileKind.Round, screen.Namespace, name, 0, 0, RoundSize, RoundSize));
                else
                {
                    FaceTileSize(screen.Width, screen.Height, out w, out h);
                    tiles.Add(new RigTile(RigTileKind.Face, screen.Namespace, name, 0, 0, w, h));
                }
            }
            foreach (var bar in settings.LedBarList())
            {
                if (bar == null) continue;
                double w, h;
                StripTileSize(StripEnds(bar), StripCentre(bar), out w, out h);
                tiles.Add(new RigTile(RigTileKind.Strip, StripId(bar.Namespace), bar.Name ?? string.Empty, 0, 0, w, h));
            }
            foreach (var matrix in settings.MatrixPanels())
            {
                var side = MatrixTileSide();
                tiles.Add(new RigTile(RigTileKind.Matrix, MatrixId(matrix), settings.MatrixName(matrix) ?? string.Empty, 0, 0, side, side));
            }
            return tiles;
        }

        /// <summary>A face's tile: <see cref="FaceWidth"/> across at the screen's own aspect, floored to the
        /// pixel, or <see cref="FaceMaxHeight"/> down and narrower for a face taller than that. A screen of
        /// no known size is drawn as the 1280 x 480 the artboard draws.</summary>
        public static void FaceTileSize(int screenWidth, int screenHeight, out double width, out double height)
        {
            double sw = screenWidth > 0 && screenHeight > 0 ? screenWidth : 1280;
            double sh = screenWidth > 0 && screenHeight > 0 ? screenHeight : 480;
            width = FaceWidth;
            height = Math.Floor(FaceWidth * sh / sw);
            if (height > FaceMaxHeight)
            {
                height = FaceMaxHeight;
                width = Math.Floor(FaceMaxHeight * sw / sh);
            }
        }

        /// <summary>
        /// A pit wall's or a phone's tile: the screen's own aspect fitted inside <see cref="PitWallWidth"/> x
        /// <see cref="PitWallHeight"/>, floored to the pixel, as the Screens page draws each at its own
        /// proportions. A screen of no known size is drawn as the artboard's: a 240 x 135 pit wall, a 76 x 135
        /// phone.
        /// </summary>
        /// <remarks>
        /// The plugin embeds both orientations of each: the 850 x 480 phone, which is the default, and the
        /// 480 x 850 one; the 1920 x 1080 pit wall and the 1080 x 1920 portrait one. A fixed size drew a
        /// landscape phone as a portrait tile and a portrait pit wall as a landscape one.
        /// </remarks>
        public static void SecondScreenTileSize(bool companion, int screenWidth, int screenHeight, out double width, out double height)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                width = companion ? CompanionWidth : PitWallWidth;
                height = companion ? CompanionHeight : PitWallHeight;
                return;
            }
            if (screenWidth * PitWallHeight >= screenHeight * PitWallWidth)
            {
                width = PitWallWidth;
                height = Math.Floor(PitWallWidth * screenHeight / screenWidth);
            }
            else
            {
                height = PitWallHeight;
                width = Math.Floor(PitWallHeight * screenWidth / screenHeight);
            }
        }

        /// <summary>The LEDs at each end of a strip, from its shape: three on a shape the panel cannot read.</summary>
        public static int StripEnds(LedBar bar)
        {
            var shape = bar == null ? null : LightShape.Parse(bar.Shape);
            return shape == null ? 3 : shape.Left;
        }

        /// <summary>The LEDs in a strip's centre, from its shape: nine on a shape the panel cannot read.</summary>
        public static int StripCentre(LedBar bar)
        {
            var shape = bar == null ? null : LightShape.Parse(bar.Shape);
            return shape == null ? 9 : shape.Centre;
        }

        /// <summary>A strip's picture as Ui.Strip draws it in <see cref="StripStyle.Rig"/>: the LEDs of a group
        /// <see cref="StripStyle.Gap"/> apart, the groups <see cref="StripStyle.GroupGap"/> apart, inside the
        /// padding and the frame.</summary>
        public static void StripTileSize(int ends, int centre, out double width, out double height)
        {
            var style = StripStyle.Rig;
            ends = Math.Max(0, ends);
            centre = Math.Max(0, centre);
            var groups = ends > 0 ? 3 : 1;
            var leds = 2 * ends + centre;
            var inGroupGaps = Math.Max(0, leds - groups);
            width = leds * style.Led + inGroupGaps * style.Gap + (groups - 1) * style.GroupGap + 2 * style.PadX + 2 * PanelMetrics.BorderWeight;
            height = style.Led + 2 * style.PadY + 2 * PanelMetrics.BorderWeight;
        }

        /// <summary>A matrix's picture as Ui.Matrix draws it in <see cref="MatrixStyle.Rig"/>, frame included.</summary>
        public static double MatrixTileSide()
        {
            return MatrixStyle.Rig.Side + 2 * PanelMetrics.BorderWeight;
        }

        /// <summary>The room a tile takes on the canvas: its picture, and its name above it.</summary>
        public static double FootprintWidth(RigTile tile) { return tile.Width; }

        public static double FootprintHeight(RigTile tile) { return NameHeight + NameGap + tile.Height; }

        // --- Where the tiles go ----------------------------------------------------------------------

        /// <summary>The empty edge round a laid-out canvas, and the gap between two tiles across and down.</summary>
        public const double LayoutMargin = 24;
        public const double LayoutGap = 28;

        /// <summary>How close a flank may stack its tiles before the rig falls back to rows: the artboard's
        /// right flank holds a matrix, a round face and a pit wall in 580 only by closing up.</summary>
        public const double LayoutGapMin = 8;

        /// <summary>
        /// Every tile of the rig where it is drawn -- where the driver put it, or where
        /// <see cref="DefaultLayout"/> puts it, and inside the canvas either way -- and the canvas's height.
        /// </summary>
        /// <remarks>
        /// The default is worked out for the whole rig, placed tiles included, so dragging one tile moves no
        /// other. The height is the default layout's: <see cref="CanvasHeight"/>, or taller where the
        /// fallback's rows need it, so a narrow column grows the canvas rather than piling tiles on its foot.
        /// A saved place is clamped rather than dropped when the canvas has narrowed or shortened since, and
        /// it is not written back: widen the window again and the tile is where it was put. The page lays the
        /// plan out at the width the canvas really has, and holds a drag to that same width.
        /// </remarks>
        public static RigPlan Plan(OpenDashSettings settings, double canvasWidth)
        {
            var tiles = Tiles(settings);
            double height;
            var defaults = DefaultLayout(tiles, canvasWidth, out height);
            var placed = new List<RigTile>();
            foreach (var tile in defaults)
            {
                int savedX, savedY;
                var x = tile.X;
                var y = tile.Y;
                if (TrySaved(settings, tile, out savedX, out savedY))
                {
                    x = savedX;
                    y = savedY;
                }
                placed.Add(tile.At(Clamp(x, FootprintWidth(tile), canvasWidth), Clamp(y, FootprintHeight(tile), height)));
            }
            return new RigPlan(placed, height);
        }

        /// <summary>Where the tiles go before anybody has arranged them, on a canvas of
        /// <see cref="CanvasHeight"/> or as tall as the fallback needs.</summary>
        public static IList<RigTile> DefaultLayout(IEnumerable<RigTile> tiles, double canvasWidth)
        {
            double height;
            return DefaultLayout(tiles, canvasWidth, out height);
        }

        /// <summary>
        /// Where the tiles go before anybody has arranged them, deterministically: the strips in a row across
        /// the top, the faces in the centre under them, the matrices at the flanks, a round face and the pit
        /// wall down the right, and the phone down the left, as Rig.dc.html arranges its nine.
        /// </summary>
        /// <remarks>
        /// The flanks split the matrices, the first half on the left, and stand <see cref="LayoutGap"/> from
        /// the faces, the three of them centred in the canvas as one group, so a wide window centres the rig
        /// rather than pulling its flanks to the edges. What sits low on a flank -- the phone on the left, the
        /// pit wall on the right -- is set level with the foot of the faces when there is room above it, and
        /// under what is higher on its flank when there is not. The faces fill the room between the flanks in
        /// rows, each row centred. A canvas too narrow for that, or a rig too tall for
        /// <see cref="CanvasHeight"/>, falls back to rows by kind across the whole width, and
        /// <paramref name="canvasHeight"/> grows to hold them with the hint clear under the last. The tiles
        /// come back in the order they were given.
        /// </remarks>
        public static IList<RigTile> DefaultLayout(IEnumerable<RigTile> tiles, double canvasWidth, out double canvasHeight)
        {
            canvasHeight = CanvasHeight;
            var list = tiles == null ? new List<RigTile>() : tiles.Where(tile => tile != null).ToList();
            var placed = new Dictionary<RigTile, RigTile>();
            if (list.Count == 0) return new List<RigTile>();

            var strips = list.Where(t => t.Kind == RigTileKind.Strip).ToList();
            var faces = list.Where(t => t.Kind == RigTileKind.Face).ToList();
            var matrices = list.Where(t => t.Kind == RigTileKind.Matrix).ToList();
            var leftMatrices = matrices.Take((matrices.Count + 1) / 2).ToList();
            var rightMatrices = matrices.Skip(leftMatrices.Count).ToList();
            var leftHigh = leftMatrices;
            var leftLow = list.Where(t => t.Kind == RigTileKind.Companion).ToList();
            var rightHigh = rightMatrices.Concat(list.Where(t => t.Kind == RigTileKind.Round)).ToList();
            var rightLow = list.Where(t => t.Kind == RigTileKind.PitWall).ToList();

            var top = LayoutMargin;
            if (strips.Count > 0)
            {
                var bottom = FlowCentred(strips, LayoutMargin, canvasWidth - LayoutMargin, top, placed);
                top = bottom + LayoutGap;
            }

            var leftWidth = Widest(leftHigh.Concat(leftLow));
            var rightWidth = Widest(rightHigh.Concat(rightLow));
            var leftPart = leftWidth > 0 ? leftWidth + LayoutGap : 0;
            var rightPart = rightWidth > 0 ? rightWidth + LayoutGap : 0;
            var room = canvasWidth - 2 * LayoutMargin - leftPart - rightPart;
            if (room > 0 && (faces.Count == 0 || room >= Widest(faces)))
            {
                // The faces' rows in the room between the flanks, and the block the widest of them makes;
                // the flanks stand a gap either side of that block, and the three are centred together.
                var block = faces.Count == 0 ? 0 : FlowRows(faces, room).Max(row => RowWidth(row));
                var group = block > 0 ? leftPart + block + rightPart : Math.Max(0, leftPart + rightPart - LayoutGap);
                var groupLeft = Math.Max(LayoutMargin, Math.Floor((canvasWidth - group) / 2));
                var blockLeft = groupLeft + leftPart;
                var facesBottom = faces.Count == 0 ? top : FlowCentred(faces, blockLeft, blockLeft + block, top, placed);
                var foot = CanvasHeight - LayoutMargin;
                var leftBottom = Column(leftHigh, leftLow, groupLeft, top, facesBottom, foot, placed);
                var rightBottom = Column(rightHigh, rightLow, groupLeft + group - rightWidth, top, facesBottom, foot, placed);
                var lowest = Math.Max(facesBottom, Math.Max(leftBottom, rightBottom));
                if (lowest <= foot) return list.Select(tile => placed[tile]).ToList();
            }

            var rows = Rows(list, canvasWidth);
            canvasHeight = Math.Max(CanvasHeight, rows.Max(tile => tile.Y + FootprintHeight(tile)) + HintClear);
            return rows;
        }

        /// <summary>A flank: what sits high on it stacked from the top, then what sits low, level with the
        /// faces' foot where it fits there. A flank too tall for the canvas at <see cref="LayoutGap"/> closes
        /// up, as far as <see cref="LayoutGapMin"/>. Returns the flank's foot.</summary>
        private static double Column(IList<RigTile> high, IList<RigTile> low, double x, double top, double facesBottom, double foot, IDictionary<RigTile, RigTile> placed)
        {
            var count = high.Count + low.Count;
            var total = high.Concat(low).Sum(tile => FootprintHeight(tile));
            var gap = count < 2 ? LayoutGap : Math.Max(LayoutGapMin, Math.Min(LayoutGap, Math.Floor((foot - top - total) / (count - 1))));
            var y = top;
            var bottom = top;
            foreach (var tile in high)
            {
                placed[tile] = tile.At(x, y);
                bottom = y + FootprintHeight(tile);
                y = bottom + gap;
            }
            if (low.Count == 0) return bottom;
            var lowHeight = low.Sum(tile => FootprintHeight(tile)) + (low.Count - 1) * gap;
            var start = high.Count == 0 ? top : bottom + gap;
            y = Math.Max(start, facesBottom - lowHeight);
            foreach (var tile in low)
            {
                placed[tile] = tile.At(x, y);
                bottom = y + FootprintHeight(tile);
                y = bottom + gap;
            }
            return bottom;
        }

        /// <summary>Tiles in rows between two edges, each row centred and wrapped when the next tile would
        /// pass the right edge. Returns the foot of the last row.</summary>
        private static double FlowCentred(IList<RigTile> tiles, double left, double right, double top, IDictionary<RigTile, RigTile> placed)
        {
            var y = top;
            var bottom = top;
            foreach (var line in FlowRows(tiles, right - left))
            {
                var width = RowWidth(line);
                var x = Math.Max(left, Math.Floor(left + (right - left - width) / 2));
                var tall = 0.0;
                foreach (var tile in line)
                {
                    placed[tile] = tile.At(x, y);
                    x += FootprintWidth(tile) + LayoutGap;
                    tall = Math.Max(tall, FootprintHeight(tile));
                }
                bottom = y + tall;
                y = bottom + LayoutGap;
            }
            return bottom;
        }

        /// <summary>Tiles in rows no wider than <paramref name="width"/>, a row wrapped when the next tile would
        /// pass it; a tile wider than that has a row of its own.</summary>
        private static List<List<RigTile>> FlowRows(IList<RigTile> tiles, double width)
        {
            var rows = new List<List<RigTile>>();
            var row = new List<RigTile>();
            var used = 0.0;
            foreach (var tile in tiles)
            {
                var need = (row.Count == 0 ? 0 : LayoutGap) + FootprintWidth(tile);
                if (row.Count > 0 && used + need > width)
                {
                    rows.Add(row);
                    row = new List<RigTile>();
                    used = 0;
                    need = FootprintWidth(tile);
                }
                row.Add(tile);
                used += need;
            }
            if (row.Count > 0) rows.Add(row);
            return rows;
        }

        private static double RowWidth(IList<RigTile> row)
        {
            return row.Sum(tile => FootprintWidth(tile)) + (row.Count - 1) * LayoutGap;
        }

        private static double Widest(IEnumerable<RigTile> tiles)
        {
            var widest = 0.0;
            foreach (var tile in tiles) widest = Math.Max(widest, FootprintWidth(tile));
            return widest;
        }

        /// <summary>
        /// The fallback: by kind, in rows across the canvas from the left, each row as tall as its tallest
        /// tile -- the strips, then the screens, then the matrices, each starting a row of its own.
        /// </summary>
        /// <remarks>
        /// Stable, so tiles of one kind keep the rig's order: List.Sort is not, and laid a rig of pit wall A,
        /// pit wall B and a rim out as the rim, B, A. A tile never starts past the canvas's width; a row that
        /// would is wrapped.
        /// </remarks>
        private static IList<RigTile> Rows(IList<RigTile> tiles, double canvasWidth)
        {
            var ordered = tiles.Select((tile, index) => new { tile, index })
                .OrderBy(entry => RowOrder(entry.tile.Kind)).ThenBy(entry => entry.index)
                .Select(entry => entry.tile).ToList();
            var placed = new Dictionary<RigTile, RigTile>();
            var x = LayoutMargin;
            var y = LayoutMargin;
            var rowHeight = 0.0;
            var lastGroup = -1;
            foreach (var tile in ordered)
            {
                var group = RowGroup(tile.Kind);
                var newRow = (lastGroup >= 0 && group != lastGroup) || (x > LayoutMargin && x + FootprintWidth(tile) > canvasWidth - LayoutMargin);
                if (newRow)
                {
                    x = LayoutMargin;
                    y += rowHeight + LayoutGap;
                    rowHeight = 0;
                }
                placed[tile] = tile.At(x, y);
                x += FootprintWidth(tile) + LayoutGap;
                rowHeight = Math.Max(rowHeight, FootprintHeight(tile));
                lastGroup = group;
            }
            return tiles.Select(tile => placed[tile]).ToList();
        }

        private static int RowOrder(RigTileKind kind)
        {
            switch (kind)
            {
                case RigTileKind.Strip: return 0;
                case RigTileKind.Face: return 1;
                case RigTileKind.Round: return 2;
                case RigTileKind.PitWall: return 3;
                case RigTileKind.Companion: return 4;
                default: return 5;
            }
        }

        private static int RowGroup(RigTileKind kind)
        {
            if (kind == RigTileKind.Strip) return 0;
            return kind == RigTileKind.Matrix ? 2 : 1;
        }

        /// <summary>One axis of a tile's place, held inside the canvas: never before its start, never so far
        /// that the tile passes its end. A tile bigger than the canvas sits at the start.</summary>
        public static double Clamp(double position, double size, double extent)
        {
            var most = extent - size;
            if (position > most) position = most;
            return position < 0 ? 0 : position;
        }

        /// <summary>A dropped tile's place, on the nearest step of the grid. Ui.DotGrid draws a step's dot at
        /// its centre, as the artboard's radial-gradient tile does, so a tile's corner falls between four dots.</summary>
        public static double Snap(double position)
        {
            return Math.Round(position / GridStep, MidpointRounding.AwayFromZero) * GridStep;
        }

        /// <summary>
        /// One axis of where a dropped tile lands: the nearest step of the grid, or the last step inside the
        /// canvas where the nearest would pass its edge, and never before its start.
        /// </summary>
        /// <remarks>
        /// Always on the grid, so <see cref="SavePosition"/> keeps exactly the place the tile is drawn at and
        /// <see cref="Plan"/> draws it there again: clamping after snapping would draw a tile flush with the
        /// edge and keep it snapped a few pixels further on, and the next build would move it.
        /// </remarks>
        public static double DropPosition(double position, double size, double extent)
        {
            var most = Math.Floor((extent - size) / GridStep) * GridStep;
            var snapped = Snap(position);
            if (snapped > most) snapped = most;
            return snapped < 0 ? 0 : snapped;
        }

        /// <summary>Where the driver put a tile, if they have: ScreenInstance's, LedBar's or the matrix
        /// slot's LayoutX and LayoutY, both set and neither negative.</summary>
        public static bool TrySaved(OpenDashSettings settings, RigTile tile, out int x, out int y)
        {
            x = 0;
            y = 0;
            int? sx = null, sy = null;
            if (settings == null || tile == null) return false;
            switch (tile.Kind)
            {
                case RigTileKind.Strip:
                    var bar = settings.LedBarByNamespace(tile.Key);
                    if (bar != null) { sx = bar.LayoutX; sy = bar.LayoutY; }
                    break;
                case RigTileKind.Matrix:
                    var i = MatrixIndex(tile);
                    if (i >= 0)
                    {
                        sx = settings.MatrixLayoutX != null && i < settings.MatrixLayoutX.Length ? settings.MatrixLayoutX[i] : null;
                        sy = settings.MatrixLayoutY != null && i < settings.MatrixLayoutY.Length ? settings.MatrixLayoutY[i] : null;
                    }
                    break;
                default:
                    var screen = settings.ScreenByNamespace(tile.Key);
                    if (screen != null) { sx = screen.LayoutX; sy = screen.LayoutY; }
                    break;
            }
            if (!sx.HasValue || !sy.HasValue || sx.Value < 0 || sy.Value < 0) return false;
            x = sx.Value;
            y = sy.Value;
            return true;
        }

        /// <summary>
        /// Keeps where a tile was dropped: snapped to the grid, never negative, in the tile's own settings.
        /// The page saves after it. Nothing here is a property a dashboard reads.
        /// </summary>
        public static void SavePosition(OpenDashSettings settings, RigTile tile, double x, double y)
        {
            if (settings == null || tile == null) return;
            var px = (int)Math.Max(0, Snap(x));
            var py = (int)Math.Max(0, Snap(y));
            switch (tile.Kind)
            {
                case RigTileKind.Strip:
                    var bar = settings.LedBarByNamespace(tile.Key);
                    if (bar != null) { bar.LayoutX = px; bar.LayoutY = py; }
                    break;
                case RigTileKind.Matrix:
                    var i = MatrixIndex(tile);
                    if (i < 0) return;
                    if (settings.MatrixLayoutX == null || settings.MatrixLayoutX.Length <= i || settings.MatrixLayoutY == null || settings.MatrixLayoutY.Length <= i) return;
                    settings.MatrixLayoutX[i] = px;
                    settings.MatrixLayoutY[i] = py;
                    break;
                default:
                    var screen = settings.ScreenByNamespace(tile.Key);
                    if (screen != null) { screen.LayoutX = px; screen.LayoutY = py; }
                    break;
            }
        }

        /// <summary>Forgets every place a driver put a tile -- the screens', the strips' and the matrices' --
        /// so the whole canvas takes the default again. Returns whether there was anything to forget.</summary>
        public static bool ClearLayout(OpenDashSettings settings)
        {
            if (settings == null) return false;
            var any = false;
            foreach (var screen in settings.RigScreens())
            {
                if (screen == null) continue;
                any |= screen.LayoutX.HasValue || screen.LayoutY.HasValue;
                screen.LayoutX = null;
                screen.LayoutY = null;
            }
            foreach (var bar in settings.LedBarList())
            {
                if (bar == null) continue;
                any |= bar.LayoutX.HasValue || bar.LayoutY.HasValue;
                bar.LayoutX = null;
                bar.LayoutY = null;
            }
            any |= ClearAll(settings.MatrixLayoutX);
            any |= ClearAll(settings.MatrixLayoutY);
            return any;
        }

        private static bool ClearAll(int?[] positions)
        {
            if (positions == null) return false;
            var any = false;
            for (var i = 0; i < positions.Length; i++)
            {
                any |= positions[i].HasValue;
                positions[i] = null;
            }
            return any;
        }

        /// <summary>The matrix slot a tile draws, as an index into the four-slot arrays, or -1.</summary>
        private static int MatrixIndex(RigTile tile)
        {
            int slot;
            if (!int.TryParse(tile.Key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out slot)) return -1;
            return slot >= 1 && slot <= Contract.FlagBoxMatrices.Count ? slot - 1 : -1;
        }

        /// <summary>The matrix slot a tile draws, or 0 when it draws none.</summary>
        public static int MatrixSlot(RigTile tile)
        {
            return tile == null || tile.Kind != RigTileKind.Matrix ? 0 : MatrixIndex(tile) + 1;
        }

        // --- What a tile shows ------------------------------------------------------------------------

        /// <summary>
        /// Whether a tile carries the warning dot after its name: the device has something on Home's list.
        /// </summary>
        /// <remarks>
        /// The artboard puts it on a strip and a matrix. A screen whose dashboard is missing or not yet in
        /// SimHub carries it too, since that is as much a device in trouble as a strip nobody selected.
        /// </remarks>
        public static bool Warns(RigTile tile, IEnumerable<PanelIssue> issues)
        {
            if (tile == null || issues == null) return false;
            var key = tile.Key;
            switch (tile.Kind)
            {
                case RigTileKind.Strip:
                    return PanelAttention.Has(issues, PanelAttention.StripUnselected, key) || PanelAttention.Has(issues, PanelAttention.StripOutdated, key);
                case RigTileKind.Matrix:
                    return PanelAttention.Has(issues, PanelAttention.MatrixDark, key) || PanelAttention.Has(issues, PanelAttention.FlagBoxOutdated);
                default:
                    return PanelAttention.Has(issues, PanelAttention.ScreenMissing, key) || PanelAttention.Has(issues, PanelAttention.ScreenRestart, key);
            }
        }

        /// <summary>What a strip is set to that changes its picture.</summary>
        public static StripOptions StripOptionsFor(LedBar bar)
        {
            var options = new StripOptions();
            if (bar == null) return options;
            options.SpotterWhole = bar.SpotterWhole;
            if (bar.EffectsOff != null)
            {
                foreach (var off in bar.EffectsOff)
                {
                    if (off != null) options.EffectsOff.Add(off);
                }
            }
            return options;
        }

        /// <summary>What a matrix panel is set to that changes its picture: its side, its idle display and
        /// its bands, and which families it shows.</summary>
        public static MatrixOptions MatrixOptionsFor(OpenDashSettings settings, int slot)
        {
            return new MatrixOptions
            {
                Side = settings.MatrixSide(slot),
                Rest = settings.MatrixRest(slot),
                Bands = settings.MatrixGearBands(slot),
                CarLadder = settings.MatrixGearCarLadder(slot),
                Flags = settings.MatrixFlags(slot),
                Pit = settings.MatrixPit(slot),
                Spotter = settings.MatrixSpotter(slot),
                Warnings = settings.MatrixWarnings(slot),
            };
        }

        /// <summary>
        /// A face's three body zones in the order the face draws them, each named for the page it is on now,
        /// with zone A's gear pages drawn as the gear. A face of no known size is drawn as the reference face.
        /// </summary>
        public static IList<RigZone> FaceZones(ScreenInstance screen)
        {
            var size = FaceShape(screen);
            var face = screen == null ? null : screen.Face;
            var zones = new List<RigZone>();
            var order = size.BodyOrder;
            for (var i = 0; i < order.Length; i++)
            {
                var letter = order[i];
                var page = face == null ? Contract.DefaultFaceZonePages[Array.IndexOf(Contract.FaceZoneLetters, letter)] : face.Zone(letter);
                var gear = letter == "A" && IsGearPage(FacePages.IdOf(letter, page));
                var weight = size.Parts != null && i < size.Parts.Length ? size.Parts[i] : 1;
                zones.Add(new RigZone(letter, gear ? PanelEmulation.Gear : FacePages.NameOf(letter, page), gear, weight));
            }
            return zones;
        }

        private static bool IsGearPage(string id)
        {
            return id == "gearSpeedRevs" || id == "gearAlone";
        }

        /// <summary>Whether a face stacks its zones down rather than across, as the 600 x 686 does.</summary>
        public static bool FaceColumn(ScreenInstance screen)
        {
            return FaceShape(screen).Body == Contract.FaceBody.Column;
        }

        private static Contract.FaceSize FaceShape(ScreenInstance screen)
        {
            var size = screen == null ? null : screen.FaceSize;
            return size ?? Contract.ReferenceFace;
        }

        /// <summary>What a face's band says when no scenario takes it: the name of the page band D is on.</summary>
        public static string FaceBandIdle(ScreenInstance screen)
        {
            var face = screen == null ? null : screen.Face;
            var page = face == null ? Contract.DefaultFaceZonePages[Array.IndexOf(Contract.FaceZoneLetters, "D")] : face.Zone("D");
            return FacePages.NameOf("D", page);
        }

        /// <summary>A face's band's height: <see cref="BandRatio"/> of the face, never under <see cref="BandMinHeight"/>.</summary>
        public static double BandHeight(double faceHeight)
        {
            return Math.Max(BandMinHeight, Math.Round(faceHeight * BandRatio, MidpointRounding.AwayFromZero));
        }

        /// <summary>The gear digit's size on a face: a third of a wide face's height (44 on the artboard's
        /// 135, 36 on its 112), and a sixth of a tall one's, whose zones are stacked.</summary>
        public static double FaceGearSize(double faceHeight, bool column)
        {
            return Math.Round(faceHeight * (column ? 0.16 : 0.325), MidpointRounding.AwayFromZero);
        }

        /// <summary>A face's rev strip under a scenario: <see cref="RevSegments"/> segments, or every one
        /// unlit where the face's rev bar is off.</summary>
        public static string[] FaceRevs(string scenarioId, string revBarMode)
        {
            if (revBarMode == Contract.RevBarOff) return new string[RevSegments];
            return PanelEmulation.Revs(RevSegments, scenarioId);
        }

        /// <summary>A round face's ring under a scenario: the revs' colour, and unlit where the rig's rev
        /// ring is off.</summary>
        public static string RingColour(string scenarioId, string revBarMode)
        {
            return revBarMode == Contract.RevBarOff ? Theme.SurfaceRaised : PanelEmulation.RingColour(scenarioId);
        }

        /// <summary>What fills a phone under a scenario: a flag takes the whole of it, as the artboard draws,
        /// and nothing else does.</summary>
        public static FaceBand CompanionBand(string scenarioId)
        {
            return PanelEmulation.IsFlag(scenarioId) ? PanelEmulation.Band(scenarioId) : FaceBand.Idle;
        }

        /// <summary>What a phone says when no flag takes it: the module it opens on.</summary>
        public static string CompanionIdle(ScreenInstance screen)
        {
            var start = screen == null ? Contract.DefaultCompanionStart : screen.CompanionStart;
            var module = start >= 0 && start < Modules.All.Count ? Modules.All[start] : Modules.All[Contract.DefaultCompanionStart];
            return module.Name;
        }

        /// <summary>The fixed panels of a pit wall page by what they show; a zone is named for its page.</summary>
        public const string BoardLabel = "Leaderboard";

        /// <summary>The portrait pit wall's one page, by the name its zones' settings carry
        /// (Contract.PitWallZoneSlots' "Portrait"), which its band shows when no flag takes it.</summary>
        public const string PortraitPage = "Portrait";

        /// <summary>The portrait page under its header, as fractions: the board takes the first 800 of 1856
        /// px, the session and lap-data row the next 112, and the four zones the rest, two by two.</summary>
        public const double PortraitBoardShare = 0.43;
        public const double PortraitZonesTop = 0.49;
        public const double PortraitCellGap = 0.02;

        /// <summary>Whether a pit wall is the portrait package, which has one page of its own rather than the
        /// landscape three.</summary>
        public static bool PitWallPortrait(ScreenInstance screen)
        {
            return screen != null && screen.Width > 0 && screen.Height > screen.Width;
        }

        /// <summary>
        /// The panels of the page a pit wall is on, as fractions of the tile's body under its band: the board
        /// or the tower by name, each zone by the page it shows. The Race page is the artboard's Leaderboard,
        /// Fuel and Tyres; the portrait pit wall draws its one page, the board over zones A to D.
        /// </summary>
        public static IList<RigCell> PitWallCells(ScreenInstance screen)
        {
            if (PitWallPortrait(screen)) return PortraitCells(screen);
            var index = Contract.NormalisePitWallPage(screen == null ? Contract.DefaultPitWallPage : screen.PitWallPage);
            var title = Contract.PitWallPageNames[index];
            var page = PanelPitWallPlan.Pages.First(p => p.Title == title);
            var spanX = PanelPitWallPlan.ThumbWidth - 2 * PanelPitWallPlan.Inset;
            var spanY = PanelPitWallPlan.ThumbHeight - 2 * PanelPitWallPlan.Inset;
            var cells = new List<RigCell>();
            foreach (var panel in page.Panels)
            {
                string text;
                if (!panel.Configurable) text = panel.Name == "Board" ? BoardLabel : panel.Name;
                else text = PitWallZoneName(screen, title + panel.Name);
                cells.Add(new RigCell(text,
                    Fraction(panel.X - PanelPitWallPlan.Inset, spanX),
                    Fraction(panel.Y - PanelPitWallPlan.Inset, spanY),
                    Fraction(panel.Width, spanX),
                    Fraction(panel.Height, spanY)));
            }
            return cells;
        }

        /// <summary>The portrait page: the board across the top, and zones A to D two by two under the session
        /// row, upper left, upper right, lower left, lower right, as PanelPitWallPlan places them.</summary>
        private static IList<RigCell> PortraitCells(ScreenInstance screen)
        {
            var half = (1 - PortraitCellGap) / 2;
            var zoneHeight = (1 - PortraitZonesTop - PortraitCellGap) / 2;
            var cells = new List<RigCell> { new RigCell(BoardLabel, 0, 0, 1, PortraitBoardShare) };
            var letters = Contract.PitWallZoneLetters;
            for (var i = 0; i < letters.Length; i++)
            {
                var x = i % 2 == 0 ? 0 : half + PortraitCellGap;
                var y = PortraitZonesTop + (i < 2 ? 0 : zoneHeight + PortraitCellGap);
                cells.Add(new RigCell(PitWallZoneName(screen, PortraitPage + letters[i]), x, y, half, zoneHeight));
            }
            return cells;
        }

        /// <summary>A pit wall zone by the page it shows: the screen's choice, or the slot's own opening page.</summary>
        private static string PitWallZoneName(ScreenInstance screen, string key)
        {
            var slot = Contract.PitWallZoneSlotByKey(key);
            var chosen = screen == null ? (slot == null ? 0 : slot.Fallback) : screen.ZonePage(key);
            var pages = slot != null && slot.Wide ? ZonePages.Wide : ZonePages.Standard;
            return chosen >= 0 && chosen < pages.Count ? pages[chosen].Name : string.Empty;
        }

        private static double Fraction(double value, double span)
        {
            var f = value / span;
            return f < 0 ? 0 : f > 1 ? 1 : f;
        }

        /// <summary>What a pit wall's band says when no scenario takes it: the page it is on, which is
        /// <see cref="PortraitPage"/> on the portrait pit wall.</summary>
        public static string PitWallBandIdle(ScreenInstance screen)
        {
            if (PitWallPortrait(screen)) return PortraitPage;
            var index = Contract.NormalisePitWallPage(screen == null ? Contract.DefaultPitWallPage : screen.PitWallPage);
            return Contract.PitWallPageNames[index];
        }
    }
}
