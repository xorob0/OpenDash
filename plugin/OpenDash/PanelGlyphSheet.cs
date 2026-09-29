// PanelGlyphSheet.cs: the flag box's glyphs as the build drew them, for the panel to paint.
//
// The Rig page paints a flag, a car alongside or a warning on every tile at once, and the Matrix page
// draws a live 8 x 8 preview (#503). Both have to show what the box will show, and the only honest source
// for that is the generator: glyphCatalogue() in packages/dash/src/leds/sheet.ts is what the profile is
// built from, so the build writes it out as build/flag-box-glyphs.json, scripts/package.sh copies it into
// Resources/, and the plugin embeds it as OpenDash.FlagBoxGlyphs.json. A second drawing of the glyphs here
// would be a second place for them to disagree.
//
// Pure: no WPF, no SimHub, and no JSON library of our choosing. System.Text.Json is not on net48 and
// Newtonsoft is SimHub's copy rather than ours, so it is read with JsonReaderWriterFactory, which is in
// the framework on net48 and net8.0 both -- the way CarLightTable.cs reads the car tables. Compiled into
// OpenDash.Tests by the Panel*.cs wildcard.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OpenDashPlugin
{
    /// <summary>One glyph of the sheet: what it is called, what family it belongs to, and its frames.</summary>
    public sealed class PanelGlyph
    {
        public PanelGlyph(string name, string kind, string[][][] frames)
        {
            Name = name;
            Kind = kind;
            Frames = frames;
        }

        /// <summary>The generator's name for it, which is what the panel asks <see cref="PanelGlyphSheet"/> for,
        /// spelled exactly as the build writes it: "yellow", "Pit speeding", "Spotter carLeft", "Gear 3 redline".</summary>
        public string Name { get; private set; }

        /// <summary>One of <see cref="PanelGlyphSheet.Kinds"/>.</summary>
        public string Kind { get; private set; }

        /// <summary>
        /// Every frame, each rows of cells, each cell "#RRGGBB" or null for a dark LED. One frame for a
        /// glyph that holds, more for one that moves, in the order the box plays them.
        /// </summary>
        public string[][][] Frames { get; private set; }
    }

    /// <summary>The whole sheet, in glyphCatalogue() order.</summary>
    public sealed class PanelGlyphSheet : IReadOnlyList<PanelGlyph>
    {
        /// <summary>What the plugin embeds the sheet as. OpenDash.csproj gives it this name.</summary>
        public const string ResourceName = "OpenDash.FlagBoxGlyphs.json";

        /// <summary>The shape of the file this reads. A sheet of another version is refused rather than
        /// half read.</summary>
        public const int SchemaVersion = 1;

        /// <summary>The families a glyph belongs to, which the Rig page's chips are named after.</summary>
        public static readonly string[] Kinds = { "flag", "pit", "spotter", "warning", "gear", "standby" };

        /// <summary>No glyphs: what a build without the sheet embedded has, and the panel draws tiles bare.</summary>
        public static readonly PanelGlyphSheet Empty = new PanelGlyphSheet(0, 0, new List<PanelGlyph>());

        private readonly List<PanelGlyph> glyphs;

        private PanelGlyphSheet(int rows, int columns, List<PanelGlyph> glyphs)
        {
            Rows = rows;
            Columns = columns;
            this.glyphs = glyphs;
        }

        /// <summary>LEDs down a frame. Eight for the flag box.</summary>
        public int Rows { get; private set; }

        /// <summary>LEDs across a frame. Eight for the flag box.</summary>
        public int Columns { get; private set; }

        public int Count { get { return glyphs.Count; } }

        public PanelGlyph this[int index] { get { return glyphs[index]; } }

        public IEnumerator<PanelGlyph> GetEnumerator() { return glyphs.GetEnumerator(); }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        /// <summary>The glyph of that name, or null when the sheet has none.</summary>
        public PanelGlyph Find(string name)
        {
            if (name == null) return null;
            foreach (var glyph in glyphs)
            {
                if (string.Equals(glyph.Name, name, StringComparison.Ordinal)) return glyph;
            }
            return null;
        }

        /// <summary>
        /// The embedded sheet, or <see cref="Empty"/> when the build did not embed one or it cannot be
        /// read. The panel draws its tiles without glyphs rather than failing to open.
        /// </summary>
        public static PanelGlyphSheet Load(Assembly assembly)
        {
            if (assembly == null) return Empty;
            try
            {
                using (var stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null) return Empty;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return Parse(reader.ReadToEnd());
                    }
                }
            }
            catch (FormatException)
            {
                return Empty;
            }
            catch (IOException)
            {
                return Empty;
            }
        }

        /// <summary>
        /// Reads a sheet, and refuses one that is not the shape the build writes: another schema
        /// version, a size that is not positive, a glyph with no name, no frames or an unknown kind, a
        /// frame that is not rows by columns, or a cell that is neither "#RRGGBB" nor null.
        /// </summary>
        /// <exception cref="FormatException">The sheet is not one this can draw.</exception>
        public static PanelGlyphSheet Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("The glyph sheet is empty.");
            XElement root;
            try
            {
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                {
                    root = XDocument.Load(reader).Root;
                }
            }
            catch (XmlException ex)
            {
                throw new FormatException("The glyph sheet is not JSON: " + ex.Message, ex);
            }
            if (root == null) throw new FormatException("The glyph sheet is empty.");

            var version = Integer(Member(root, "schemaVersion"), "schemaVersion");
            if (version != SchemaVersion) throw new FormatException("The glyph sheet is schema " + version + ", not " + SchemaVersion + ".");
            var rows = Integer(Member(root, "rows"), "rows");
            var columns = Integer(Member(root, "columns"), "columns");
            if (rows <= 0 || columns <= 0) throw new FormatException("The glyph sheet has no LEDs.");

            var list = Member(root, "glyphs");
            if (list == null || !IsArray(list)) throw new FormatException("The glyph sheet lists no glyphs.");
            var glyphs = new List<PanelGlyph>();
            foreach (var item in list.Elements()) glyphs.Add(Glyph(item, rows, columns));
            return new PanelGlyphSheet(rows, columns, glyphs);
        }

        private static PanelGlyph Glyph(XElement item, int rows, int columns)
        {
            var name = Text(Member(item, "name"));
            if (string.IsNullOrEmpty(name)) throw new FormatException("A glyph has no name.");
            var kind = Text(Member(item, "kind"));
            if (Array.IndexOf(Kinds, kind) < 0) throw new FormatException("Glyph " + name + " is of no kind the panel knows: " + (kind ?? "none") + ".");
            var framesElement = Member(item, "frames");
            if (framesElement == null || !IsArray(framesElement)) throw new FormatException("Glyph " + name + " has no frames.");
            var frames = new List<string[][]>();
            foreach (var frame in framesElement.Elements()) frames.Add(Frame(frame, name, rows, columns));
            if (frames.Count == 0) throw new FormatException("Glyph " + name + " has no frames.");
            return new PanelGlyph(name, kind, frames.ToArray());
        }

        private static string[][] Frame(XElement frame, string name, int rows, int columns)
        {
            var lines = IsArray(frame) ? frame.Elements().ToList() : null;
            if (lines == null || lines.Count != rows) throw new FormatException("A frame of " + name + " is not " + rows + " rows.");
            var result = new string[rows][];
            for (var r = 0; r < rows; r++)
            {
                var cells = IsArray(lines[r]) ? lines[r].Elements().ToList() : null;
                if (cells == null || cells.Count != columns) throw new FormatException("A row of " + name + " is not " + columns + " LEDs.");
                result[r] = new string[columns];
                for (var c = 0; c < columns; c++) result[r][c] = Cell(cells[c], name);
            }
            return result;
        }

        private static string Cell(XElement cell, string name)
        {
            if (Type(cell) == "null") return null;
            var value = Type(cell) == "string" ? cell.Value : null;
            if (!IsColour(value)) throw new FormatException("A cell of " + name + " is neither a colour nor dark: " + (value ?? cell.Value) + ".");
            return value;
        }

        /// <summary>"#RRGGBB", in either case.</summary>
        private static bool IsColour(string value)
        {
            if (value == null || value.Length != 7 || value[0] != '#') return false;
            for (var i = 1; i < 7; i++)
            {
                var ch = value[i];
                var hex = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        private static XElement Member(XElement parent, string name)
        {
            return parent.Elements().FirstOrDefault(e => string.Equals((string)e.Attribute("item") ?? e.Name.LocalName, name, StringComparison.Ordinal));
        }

        private static string Type(XElement element)
        {
            return (string)element.Attribute("type");
        }

        private static bool IsArray(XElement element)
        {
            return Type(element) == "array";
        }

        private static string Text(XElement element)
        {
            return element == null || Type(element) != "string" ? null : element.Value;
        }

        private static int Integer(XElement element, string name)
        {
            int value;
            if (element == null || Type(element) != "number" || !int.TryParse(element.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                throw new FormatException("The glyph sheet's " + name + " is not a whole number.");
            }
            return value;
        }
    }
}
