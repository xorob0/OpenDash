// PanelGlyphSheetTests.cs: the glyph sheet the build writes, as the panel reads it.
//
// The fixtures are inline and small, so that what is being held is the schema rather than the catalogue:
// two glyphs, one with a dark LED and one that moves. When the build has written the real sheet it is read
// too, since what the panel has to survive is the file the generator actually emits.
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelGlyphSheetTests
    {
        private const string Y = "\"#FFD400\"";
        private const string K = "null";

        /// <summary>A frame of eight rows of eight, every cell the given one except the first, which is dark.</summary>
        private static string Frame(string cell, int rows = 8, int columns = 8)
        {
            var row = "[" + K + string.Concat(Enumerable.Repeat("," + cell, columns - 1)) + "]";
            return "[" + string.Join(",", Enumerable.Repeat(row, rows)) + "]";
        }

        private static string Sheet(string glyphs, int version = 1, int rows = 8, int columns = 8)
        {
            return "{\"schemaVersion\":" + version + ",\"rows\":" + rows + ",\"columns\":" + columns + ",\"glyphs\":[" + glyphs + "]}";
        }

        private static readonly string TwoGlyphs = Sheet(
            "{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + Frame(Y) + "]},"
            + "{\"name\":\"spotter-left\",\"kind\":\"spotter\",\"frames\":[" + Frame(Y) + "," + Frame("\"#ff8000\"") + "]}");

        [Fact]
        public void A_sheet_of_two_glyphs_reads_as_the_build_wrote_it()
        {
            var sheet = PanelGlyphSheet.Parse(TwoGlyphs);
            Assert.Equal(8, sheet.Rows);
            Assert.Equal(8, sheet.Columns);
            Assert.Equal(2, sheet.Count);
            Assert.Equal(new[] { "yellow", "spotter-left" }, sheet.Select(g => g.Name));

            var yellow = sheet.Find("yellow");
            Assert.Equal("flag", yellow.Kind);
            Assert.Single(yellow.Frames);
            Assert.Equal(8, yellow.Frames[0].Length);
            Assert.All(yellow.Frames[0], row => Assert.Equal(8, row.Length));
            // A dark LED is null, not a colour: the panel draws the unlit LED for it.
            Assert.Null(yellow.Frames[0][0][0]);
            Assert.Equal("#FFD400", yellow.Frames[0][0][1]);

            // A glyph that moves has its frames in the order the box plays them.
            var spotter = sheet.Find("spotter-left");
            Assert.Equal(2, spotter.Frames.Length);
            Assert.Equal("#ff8000", spotter.Frames[1][7][7]);
            Assert.Same(spotter, sheet[1]);

            Assert.Null(sheet.Find("green"));
            Assert.Null(sheet.Find(null));
        }

        [Fact]
        public void A_frame_that_is_not_rows_by_columns_is_refused()
        {
            // A row one LED short.
            var shortRow = Frame(Y).Replace("[" + K + "," + Y + ",", "[" + K + ",");
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet("{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + shortRow + "]}")));
            // A frame a row short.
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet("{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + Frame(Y, rows: 7) + "]}")));
            // A sheet that says it is smaller than its frames.
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet("{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + Frame(Y) + "]}", rows: 6)));
        }

        [Fact]
        public void Anything_else_the_panel_cannot_draw_is_refused_too()
        {
            var good = "{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + Frame(Y) + "]}";
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet(good, version: 2)));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet(good.Replace("\"flag\"", "\"sparkle\""))));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet(good.Replace("\"yellow\"", "\"\""))));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet("{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[]}")));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet("{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + Frame("\"yellow\"") + "]}")));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet("{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + Frame("\"#FFD4\"") + "]}")));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(Sheet("{\"name\":\"yellow\",\"kind\":\"flag\",\"frames\":[" + Frame("7") + "]}")));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse("{\"schemaVersion\":1,\"rows\":8,\"columns\":8}"));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse("{not json"));
            Assert.Throws<FormatException>(() => PanelGlyphSheet.Parse(""));
            // An empty list is a sheet, just one with nothing on it.
            Assert.Empty(PanelGlyphSheet.Parse(Sheet(string.Empty)));
        }

        [Fact]
        public void An_assembly_without_the_sheet_loads_as_no_glyphs()
        {
            // The test assembly embeds no sheet, which is a plugin built before the dash build wrote one:
            // the panel draws its tiles bare rather than failing to open.
            var sheet = PanelGlyphSheet.Load(typeof(PanelGlyphSheetTests).Assembly);
            Assert.Empty(sheet);
            Assert.Same(PanelGlyphSheet.Empty, sheet);
            Assert.Same(PanelGlyphSheet.Empty, PanelGlyphSheet.Load(null));
            Assert.Equal("OpenDash.FlagBoxGlyphs.json", PanelGlyphSheet.ResourceName);
        }

        [Fact]
        public void The_plugin_embeds_the_sheet_under_the_name_the_panel_loads()
        {
            var project = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.csproj"));
            Assert.Contains("Include=\"Resources/flag-box-glyphs.json\" LogicalName=\"" + PanelGlyphSheet.ResourceName + "\"", project);
        }

        [Fact]
        public void The_sheet_the_build_wrote_is_every_glyph_at_eight_by_eight()
        {
            string path = null;
            foreach (var candidate in new[] { Path.Combine(RepoPaths.BuildOutput(), "flag-box-glyphs.json"), Path.Combine(RepoPaths.EmbeddedResources(), "flag-box-glyphs.json") })
            {
                if (File.Exists(candidate))
                {
                    path = candidate;
                    break;
                }
            }
            if (path == null) return; // the dash build has not written it in this checkout

            var sheet = PanelGlyphSheet.Parse(File.ReadAllText(path, Encoding.UTF8));
            Assert.True(sheet.Count > 20, "only " + sheet.Count + " glyphs in " + path);
            Assert.Equal(8, sheet.Rows);
            Assert.Equal(8, sheet.Columns);
            Assert.All(sheet, glyph => Assert.All(glyph.Frames, frame =>
            {
                Assert.Equal(8, frame.Length);
                Assert.All(frame, row => Assert.Equal(8, row.Length));
            }));
            Assert.Equal(PanelGlyphSheet.Kinds.OrderBy(k => k, StringComparer.Ordinal), sheet.Select(g => g.Kind).Distinct().OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(sheet.Count, sheet.Select(g => g.Name).Distinct().Count());
        }
    }
}
